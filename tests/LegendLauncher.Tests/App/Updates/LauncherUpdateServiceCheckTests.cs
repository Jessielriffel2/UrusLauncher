using LegendLauncher.App.Updates;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace LegendLauncher.Tests.App.Updates;

public sealed class LauncherUpdateServiceCheckTests
{
    [Fact]
    public async Task NewerReleaseReturnsValidatedLocalizedState()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        UpdateTestData.EnqueueCheck(handler, manifest);
        var starter = new RecordingUpdateProcessStarter();
        var service = CreateService(handler, directory, starter);

        LauncherUpdateRelease? release = await service.CheckForUpdateAsync(new Version(1, 2, 2));

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 2, 3), release.Version);
        Assert.Equal("v1.2.3", release.TagName);
        Assert.Equal("Notas em português", release.GetNotes("pt-BR"));
        Assert.Equal("English notes", release.GetNotes("en-US"));
        Assert.Equal("Notas en español", release.GetNotes("es-ES"));
        Assert.Equal("Notas em português", release.GetNotes("unknown"));
        Assert.Equal("UrusLauncher-Setup-1.2.3-win-x64.exe", release.Installer.Name);
        Assert.Equal(UpdateTestData.InstallerBytes.LongLength, release.Installer.Bytes);
        Assert.Equal(UpdateTestData.InstallerSha256(), release.Installer.Sha256);
        Assert.Equal(2, handler.RequestedUris.Count);
        Assert.Equal(LauncherUpdateValidation.LatestReleaseUri, handler.RequestedUris[0]);
        Assert.Equal(0, starter.CallCount);
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.0")]
    [InlineData("1.2.4")]
    [InlineData("2.0.0")]
    public async Task EqualOrOlderReleaseReturnsNull(string currentVersion)
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        UpdateTestData.EnqueueCheck(handler, UpdateTestData.CreateManifest());
        var service = CreateService(handler, directory);

        LauncherUpdateRelease? release =
            await service.CheckForUpdateAsync(Version.Parse(currentVersion));

        Assert.Null(release);
        Assert.Equal(2, handler.RequestedUris.Count);
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("v1.2.3.4")]
    [InlineData("1.2.3")]
    [InlineData("v01.2.3")]
    [InlineData("v1.2.3-beta")]
    public async Task InvalidReleaseTagIsRejected(string tagName)
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject release = UpdateTestData.CreateRelease(manifest, tagName);
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(release)));
        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));
    }

    [Fact]
    public async Task ManifestVersionDifferentFromTagIsRejected()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest("1.2.4");
        JsonObject release = UpdateTestData.CreateRelease(manifest, "v1.2.3");
        UpdateTestData.EnqueueCheck(handler, manifest, release);
        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("repository")]
    [InlineData("installer-name")]
    [InlineData("installer-size")]
    [InlineData("installer-hash")]
    [InlineData("notes")]
    public async Task InvalidManifestFieldsAreRejected(string invalidField)
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        switch (invalidField)
        {
            case "schema":
                manifest["schema"] = 2;
                break;
            case "repository":
                manifest["repository"] = "attacker/project";
                break;
            case "installer-name":
                manifest["installer"]!["name"] = "update.exe";
                break;
            case "installer-size":
                manifest["installer"]!["bytes"] = 0;
                break;
            case "installer-hash":
                manifest["installer"]!["sha256"] = "abcd";
                break;
            case "notes":
                manifest["notes"]!.AsObject().Remove("es-ES");
                break;
        }

        JsonObject release = CreateReleaseForPossiblyInvalidManifest(manifest);
        UpdateTestData.EnqueueCheck(handler, manifest, release);
        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));
    }

    [Fact]
    public async Task MissingOrDuplicateRequiredAssetsAreRejected()
    {
        using var directory = new TemporaryUpdateDirectory();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject missingRelease = UpdateTestData.CreateRelease(manifest);
        missingRelease["assets"]!.AsArray().RemoveAt(1);
        using (var missingHandler = new QueueHttpMessageHandler())
        {
            UpdateTestData.EnqueueCheck(missingHandler, manifest, missingRelease);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CreateService(missingHandler, directory)
                    .CheckForUpdateAsync(new Version(1, 0, 0)));
        }

        JsonObject duplicateRelease = UpdateTestData.CreateRelease(manifest);
        JsonNode duplicateManifestAsset = duplicateRelease["assets"]![0]!.DeepClone();
        duplicateRelease["assets"]!.AsArray().Add(duplicateManifestAsset);
        using var duplicateHandler = new QueueHttpMessageHandler();
        duplicateHandler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(duplicateRelease)));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService(duplicateHandler, directory)
                .CheckForUpdateAsync(new Version(1, 0, 0)));
    }

    [Theory]
    [InlineData("http://github.com/Jessielriffel2/UrusLauncher/releases/download/v1.2.3/update-manifest.json")]
    [InlineData("https://evil.example/update-manifest.json")]
    [InlineData("https://github.com/other/repo/releases/download/v1.2.3/update-manifest.json")]
    [InlineData("https://github.com/Jessielriffel2/UrusLauncher/releases/download/v9.9.9/update-manifest.json")]
    public async Task InvalidManifestAssetUrlIsRejected(string url)
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject release = UpdateTestData.CreateRelease(manifest);
        release["assets"]![0]!["browser_download_url"] = url;
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(release)));
        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));
        Assert.Single(handler.RequestedUris);
    }

    [Fact]
    public async Task InstallerAssetSizeAndDigestMustMatchManifest()
    {
        using var directory = new TemporaryUpdateDirectory();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject sizeRelease = UpdateTestData.CreateRelease(manifest);
        sizeRelease["assets"]![1]!["size"] = 999;
        using (var sizeHandler = new QueueHttpMessageHandler())
        {
            UpdateTestData.EnqueueCheck(sizeHandler, manifest, sizeRelease);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CreateService(sizeHandler, directory)
                    .CheckForUpdateAsync(new Version(1, 0, 0)));
        }

        JsonObject digestRelease = UpdateTestData.CreateRelease(manifest);
        digestRelease["assets"]![1]!["digest"] = $"sha256:{new string('0', 64)}";
        using var digestHandler = new QueueHttpMessageHandler();
        UpdateTestData.EnqueueCheck(digestHandler, manifest, digestRelease);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService(digestHandler, directory)
                .CheckForUpdateAsync(new Version(1, 0, 0)));
    }

    [Theory]
    [InlineData("size")]
    [InlineData("digest")]
    public async Task ManifestAssetMetadataMustMatchDownloadedManifest(string field)
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject release = UpdateTestData.CreateRelease(manifest);
        if (field == "size")
        {
            release["assets"]![0]!["size"] = 1;
        }
        else
        {
            release["assets"]![0]!["digest"] = $"sha256:{new string('0', 64)}";
        }

        UpdateTestData.EnqueueCheck(handler, manifest, release);
        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));
    }

    [Fact]
    public async Task MissingOptionalGitHubDigestsAreAccepted()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject release = UpdateTestData.CreateRelease(
            manifest,
            includeDigests: false);
        UpdateTestData.EnqueueCheck(handler, manifest, release);
        var service = CreateService(handler, directory);

        LauncherUpdateRelease? update =
            await service.CheckForUpdateAsync(new Version(1, 0, 0));

        Assert.NotNull(update);
    }

    [Fact]
    public async Task AllowedGitHubRedirectIsFollowedManually()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject release = UpdateTestData.CreateRelease(manifest);
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(release)));
        handler.Enqueue(UpdateTestData.Redirect("https://release-assets.githubusercontent.com/opaque-object"));
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(manifest)));
        var service = CreateService(handler, directory);

        LauncherUpdateRelease? update =
            await service.CheckForUpdateAsync(new Version(1, 0, 0));

        Assert.NotNull(update);
        Assert.Equal(3, handler.RequestedUris.Count);
        Assert.Equal("release-assets.githubusercontent.com", handler.RequestedUris[2].Host);
    }

    [Fact]
    public async Task RedirectToUntrustedHostIsRejectedBeforeRequest()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject release = UpdateTestData.CreateRelease(manifest);
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(release)));
        handler.Enqueue(UpdateTestData.Redirect("https://evil.example/payload"));
        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));
        Assert.Equal(2, handler.RequestedUris.Count);
    }

    [Fact]
    public async Task MoreThanFiveRedirectsIsRejected()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject release = UpdateTestData.CreateRelease(manifest);
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(release)));
        for (int index = 0; index < 6; index++)
        {
            handler.Enqueue(UpdateTestData.Redirect(
                $"https://release-assets.githubusercontent.com/object-{index}"));
        }

        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));
        Assert.Equal(7, handler.RequestedUris.Count);
    }

    [Fact]
    public async Task CheckTimeoutBecomesClearHttpFailure()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        var service = new LauncherUpdateService(
            new HttpClient(handler),
            directory.Path,
            checkTimeout: TimeSpan.FromMilliseconds(20));

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalCheckCancellationRemainsCancellation()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("unreachable");
        });
        var service = CreateService(handler, directory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0), cancellation.Token));
    }

    private static LauncherUpdateService CreateService(
        QueueHttpMessageHandler handler,
        TemporaryUpdateDirectory directory,
        IUpdateProcessStarter? starter = null) =>
        new(new HttpClient(handler), directory.Path, starter);

    private static JsonObject CreateReleaseForPossiblyInvalidManifest(JsonObject manifest)
    {
        JsonObject normalized = UpdateTestData.CreateManifest();
        JsonObject release = UpdateTestData.CreateRelease(normalized);
        byte[] bytes = UpdateTestData.Serialize(manifest);
        release["assets"]![0]!["size"] = bytes.LongLength;
        release["assets"]![0]!["digest"] = null;
        return release;
    }
}
