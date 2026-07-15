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
        Assert.Contains("[System.Security.Cryptography.SHA256]::Create()", source);
        Assert.DoesNotContain("Get-FileHash", source, StringComparison.Ordinal);
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
