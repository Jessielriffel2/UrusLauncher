using LegendLauncher.Core.Models;
using LegendLauncher.GameHost.Legacy;

namespace LegendLauncher.Tests.GameHost;

public sealed class GameHostOptionsTests
{
    [Fact]
    public void TryParse_AcceptsDiagnosticsWithRuntimeRoot()
    {
        string root = Path.GetTempPath();

        bool success = GameHostOptions.TryParse(
            ["--runtime-root", root, "--diagnostics"],
            out GameHostOptions? options,
            out string? error);

        Assert.True(success, error);
        Assert.NotNull(options);
        Assert.True(options.DiagnosticsOnly);
        Assert.Equal(Path.GetFullPath(root), options.RuntimeRoot);
        Assert.Null(options.PipeName);
    }

    [Fact]
    public void TryParse_AcceptsProtectedSessionChannelAndSafeRenderingOptions()
    {
        string pipeName = LaunchSessionPipeIdentity.CreatePipeName();
        string nonce = LaunchSessionPipeIdentity.CreateNonce();

        bool success = GameHostOptions.TryParse(
            [
                "--runtime-root", Path.GetTempPath(),
                "--pipe", pipeName,
                "--nonce", nonce,
                "--parent-pid", Environment.ProcessId.ToString(),
                "--quality", "autohigh",
                "--wmode", "direct",
            ],
            out GameHostOptions? options,
            out string? error);

        Assert.True(success, error);
        Assert.NotNull(options);
        Assert.False(options.DiagnosticsOnly);
        Assert.Equal(pipeName, options.PipeName);
        Assert.Equal(nonce, options.Nonce);
        Assert.Equal(Environment.ProcessId, options.ParentProcessId);
        Assert.Equal(GameRenderQuality.AutoHigh, options.Quality);
        Assert.Equal(GameWindowMode.Direct, options.WindowMode);
        Assert.DoesNotContain(nonce, options.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--url")]
    [InlineData("--token")]
    [InlineData("--password")]
    [InlineData("--session")]
    public void TryParse_RejectsSecretsAndAddressArguments(string unsafeOption)
    {
        const string sensitiveValue = "secret-that-must-not-be-echoed";

        bool success = GameHostOptions.TryParse(
            ["--runtime-root", Path.GetTempPath(), "--diagnostics", unsafeOption, sensitiveValue],
            out GameHostOptions? options,
            out string? error);

        Assert.False(success);
        Assert.Null(options);
        Assert.Contains("never accepted", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sensitiveValue, error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_DoesNotEchoUnknownArgumentValues()
    {
        const string sensitiveValue = "https://example.invalid/?token=do-not-echo";

        bool success = GameHostOptions.TryParse(
            ["--runtime-root", Path.GetTempPath(), "--diagnostics", sensitiveValue],
            out _,
            out string? error);

        Assert.False(success);
        Assert.DoesNotContain(sensitiveValue, error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_RequiresCompleteSessionIdentityOutsideDiagnostics()
    {
        bool success = GameHostOptions.TryParse(
            [
                "--runtime-root", Path.GetTempPath(),
                "--pipe", LaunchSessionPipeIdentity.CreatePipeName(),
                "--nonce", LaunchSessionPipeIdentity.CreateNonce(),
            ],
            out GameHostOptions? options,
            out string? error);

        Assert.False(success);
        Assert.Null(options);
        Assert.Contains("parent-pid", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Discover_ReportsMissingCompatibilityAssets()
    {
        string emptyDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDirectory);

        try
        {
            LegacyRuntimeAssets assets = LegacyRuntimeAssets.Discover(emptyDirectory);

            Assert.False(assets.IsComplete);
            Assert.Equal(2, assets.MissingFiles.Count);
        }
        finally
        {
            Directory.Delete(emptyDirectory);
        }
    }

    [Fact]
    public void Discover_UsesOcxPathFromManifestInsteadOfHardcodedVersion()
    {
        string runtime = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string flashDirectory = Path.Combine(runtime, "flash");
        Directory.CreateDirectory(flashDirectory);
        string ocxPath = Path.Combine(flashDirectory, "Flash64_custom.ocx");
        File.WriteAllText(ocxPath, "test");
        File.WriteAllText(
            Path.Combine(runtime, "Adobe.Flash.Control.manifest"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
              <assemblyIdentity type="win32" name="Adobe.Flash.Control" version="15.0.0.167" />
              <file name="flash\Flash64_custom.ocx" />
            </assembly>
            """);

        try
        {
            LegacyRuntimeAssets assets = LegacyRuntimeAssets.Discover(runtime);

            Assert.True(assets.IsComplete);
            Assert.Equal(ocxPath, assets.FlashOcxPath);
        }
        finally
        {
            Directory.Delete(runtime, recursive: true);
        }
    }

    [Fact]
    public void Discover_RejectsManifestOcxOutsideRuntimeRoot()
    {
        string parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string runtime = Path.Combine(parent, "runtime");
        Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(parent, "outside.ocx"), "test");
        File.WriteAllText(
            Path.Combine(runtime, "Adobe.Flash.Control.manifest"),
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
              <assemblyIdentity type="win32" name="Adobe.Flash.Control" version="15.0.0.167" />
              <file name="..\outside.ocx" />
            </assembly>
            """);

        try
        {
            LegacyRuntimeAssets assets = LegacyRuntimeAssets.Discover(runtime);

            Assert.False(assets.IsComplete);
            Assert.Null(assets.FlashOcxPath);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void ActivationContext_UsesKnownManifestWithoutRegisteringTheOcx()
    {
        string runtimeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Legend Online Client by Brov (H2_x64)");
        LegacyRuntimeAssets assets = LegacyRuntimeAssets.Discover(runtimeRoot);
        if (!assets.IsComplete)
        {
            return;
        }

        using RegistrationFreeActivationContext context =
            RegistrationFreeActivationContext.Activate(assets);

        Assert.NotNull(context);
    }
}
