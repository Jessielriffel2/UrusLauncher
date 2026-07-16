namespace LegendLauncher.Tests.App;

public sealed class WindowsDistributionContractTests
{
    [Fact]
    public void InnoInstaller_IsPerUserMultilingualAndUsesTheUrusBrand()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "installer",
            "UrusLauncher.iss"));

        Assert.Contains("#define AppName \"Urus Launcher\"", source);
        Assert.Contains("#define AppExeName \"UrusLauncher.App.exe\"", source);
        Assert.Contains("PrivilegesRequired=lowest", source);
        Assert.Contains("DefaultDirName={localappdata}\\Programs\\{#AppName}", source);
        Assert.Contains("Name: \"english\"", source);
        Assert.Contains("Name: \"brazilianportuguese\"", source);
        Assert.Contains("Name: \"spanish\"", source);
        Assert.Contains("SetupIconFile={#BrandingIcon}", source);
        Assert.Contains("Source: \"{#PublishRoot}\\*\"", source);
        Assert.Contains("Flags: ignoreversion recursesubdirs createallsubdirs", source);
        Assert.Contains("Tasks: desktopicon", source);
        Assert.Contains("Flags: nowait postinstall skipifsilent", source);
        Assert.Contains("Check: RelaunchRequested", source);
        Assert.Contains("'/RELAUNCH'", source);
        Assert.DoesNotContain(
            "FileExists(ExpandConstant('{#PublishRoot}",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DistributionScript_PublishesBothProcessesSelfContainedAndCreatesHandoffFiles()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "build-urus-distribution.ps1"));

        Assert.Contains("LegendLauncher.App\\LegendLauncher.App.csproj", source);
        Assert.Contains(
            "LegendLauncher.GameHost.Legacy\\LegendLauncher.GameHost.Legacy.csproj",
            source);
        Assert.Equal(2, CountOccurrences(source, "'--self-contained', 'true'"));
        Assert.Contains("Assert-SelfContainedApplication $payloadDirectory 'UrusLauncher.App'", source);
        Assert.Contains(
            "Assert-SelfContainedApplication $payloadDirectory 'LegendLauncher.GameHost.Legacy'",
            source);
        Assert.Contains("UrusLauncher.App.exe", source);
        Assert.Contains("LegendLauncher.GameHost.Legacy.exe", source);
        Assert.Contains("UrusLauncher-Setup-$Version-win-x64.exe", source);
        Assert.Contains("UrusLauncher-$Version-portable-win-x64.zip", source);
        Assert.Contains("distribution-manifest.json", source);
        Assert.Contains("SHA256SUMS.txt", source);
        Assert.Contains("update-manifest.json", source);
        Assert.Contains("RELEASE_NOTES.md", source);
        Assert.Contains("repository = 'Jessielriffel2/UrusLauncher'", source);
        Assert.Contains("[switch]$SkipPortableStartupSmoke", source);
        Assert.Contains("[ValidatePattern('^\\d+\\.\\d+\\.\\d+$')]", source);
        Assert.Contains("Refusing to modify a path outside", source);
        Assert.Contains("obsolete LegendLauncher.App.exe was found", source);
        Assert.Contains("$gameHostPayloadFiles = @(", source);
        Assert.Contains("'LegendLauncher.GameHost.Legacy.runtimeconfig.json'", source);
        Assert.DoesNotContain(
            "Get-ChildItem -LiteralPath $gameHostStaging -Force",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Assert-WpfWindowsBase $payloadDirectory", source);
        Assert.Contains("$windowsBase.Length -le 1MB", source);
        Assert.Contains("Test-PortableLauncherStartup $payloadDirectory", source);
        Assert.Contains("DOTNET_MULTILEVEL_LOOKUP = '0'", source);
        Assert.Contains("$process.MainWindowTitle -ne 'Urus Launcher'", source);
        Assert.Contains("'PresentationFramework.dll'", source);
        Assert.Contains("[string]$LegacyRuntimeSource", source);
        Assert.Contains("Resolve-LegacyRuntimeSource $LegacyRuntimeSource", source);
        Assert.Contains("Copy-LegacyRuntimePayload", source);
        Assert.Contains("Adobe.Flash.Control.manifest", source);
        Assert.Contains("Get-AuthenticodeSignature", source);
        Assert.Contains("(Join-Path $PayloadDirectory 'runtime')", source);
        Assert.Contains("legacyRuntime = [ordered]@{", source);
        Assert.Contains("[System.Security.Cryptography.SHA256]::Create()", source);
        Assert.DoesNotContain("Get-FileHash", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DistributedSources_DoNotEmbedADeveloperWindowsProfilePath()
    {
        string root = FindRepositoryRoot();
        string[] productionRoots =
        [
            Path.Combine(root, "src"),
            Path.Combine(root, "scripts"),
            Path.Combine(root, "installer"),
            Path.Combine(root, ".github"),
        ];
        string[] allowedExtensions = [".cs", ".xaml", ".json", ".ps1", ".iss", ".yml"];

        string[] filesWithAbsoluteUserPath = productionRoots
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            .Where(path => allowedExtensions.Contains(
                Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains("C:\\Users\\", StringComparison.OrdinalIgnoreCase) ||
                    source.Contains("C:/Users/", StringComparison.OrdinalIgnoreCase);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(filesWithAbsoluteUserPath);
    }

    [Fact]
    public void AppUsesASessionLocalSingleInstanceGuard()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "LegendLauncher.App",
            "App.xaml.cs"));

        Assert.Contains(@"Local\UrusLauncher.App.SingleInstance", source, StringComparison.Ordinal);
        Assert.Contains("new Mutex(", source, StringComparison.Ordinal);
        Assert.Contains("TryActivateExistingInstance();", source, StringComparison.Ordinal);
        Assert.Contains("candidate.SessionId == currentSessionId", source, StringComparison.Ordinal);
        Assert.Contains("if (IsIconic(window))", source, StringComparison.Ordinal);
        Assert.Contains("_singleInstanceMutex.ReleaseMutex();", source, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryFile(params string[] relativeSegments) =>
        Path.Combine([FindRepositoryRoot(), .. relativeSegments]);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LegendLauncherNext.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "LegendLauncherNext repository root was not found.");
    }
}
