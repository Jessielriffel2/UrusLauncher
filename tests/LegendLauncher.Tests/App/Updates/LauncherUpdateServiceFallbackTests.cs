using LegendLauncher.App.Updates;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace LegendLauncher.Tests.App.Updates;

public sealed class LauncherUpdateServiceFallbackTests
{
    [Fact]
    public async Task Version112Detects113ThroughPublicManifestFallback()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest("1.1.3");
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(manifest)));
        var service = CreateService(handler, directory);

        LauncherUpdateRelease? release = await service.CheckForUpdateAsync(
            new Version(1, 1, 2));

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 1, 3), release.Version);
        Assert.Equal("v1.1.3", release.TagName);
        Assert.Equal(
            "UrusLauncher-Setup-1.1.3-win-x64.exe",
            release.Installer.Name);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task PublicApiRateLimitUsesValidatedLatestManifestFallback(
        HttpStatusCode statusCode)
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        handler.Enqueue(new HttpResponseMessage(statusCode));
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(manifest)));
        var service = CreateService(handler, directory);

        LauncherUpdateRelease? release =
            await service.CheckForUpdateAsync(new Version(1, 0, 0));

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 2, 3), release.Version);
        Assert.Equal("v1.2.3", release.TagName);
        Assert.Equal("Notas em português", release.GetNotes("pt-BR"));
        Assert.Equal(
            "https://github.com/Jessielriffel2/UrusLauncher/releases/download/v1.2.3/UrusLauncher-Setup-1.2.3-win-x64.exe",
            release.Installer.DownloadUri.AbsoluteUri);
        Assert.Equal(
            [
                LauncherUpdateValidation.LatestReleaseUri,
                LauncherUpdateValidation.LatestManifestUri,
            ],
            handler.RequestedUris);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(1, 2, 4)]
    public async Task FallbackDoesNotOfferSameOrOlderVersion(
        int major,
        int minor,
        int build)
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(manifest)));
        var service = CreateService(handler, directory);

        LauncherUpdateRelease? release =
            await service.CheckForUpdateAsync(new Version(major, minor, build));

        Assert.Null(release);
        Assert.Equal(2, handler.RequestedUris.Count);
    }

    [Fact]
    public async Task LatestManifestFallbackFollowsAllowedRedirect()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
        handler.Enqueue(UpdateTestData.Redirect(
            "https://github.com/Jessielriffel2/UrusLauncher/releases/download/v1.2.3/update-manifest.json"));
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(manifest)));
        var service = CreateService(handler, directory);

        LauncherUpdateRelease? release =
            await service.CheckForUpdateAsync(new Version(1, 0, 0));

        Assert.NotNull(release);
        Assert.Equal(3, handler.RequestedUris.Count);
        Assert.Equal(
            "/Jessielriffel2/UrusLauncher/releases/download/v1.2.3/update-manifest.json",
            handler.RequestedUris[2].AbsolutePath);
    }

    [Fact]
    public async Task Http500DoesNotUseFallback()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(handler, directory);

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Single(handler.RequestedUris);
    }

    [Fact]
    public async Task InvalidPublicApiJsonDoesNotUseFallback()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue(UpdateTestData.Response("not-json"u8.ToArray()));
        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));

        Assert.Single(handler.RequestedUris);
    }

    [Fact]
    public async Task RateLimitAfterApiResponseDoesNotUseFallback()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        JsonObject release = UpdateTestData.CreateRelease(manifest);
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(release)));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var service = CreateService(handler, directory);

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(2, handler.RequestedUris.Count);
        Assert.DoesNotContain(
            LauncherUpdateValidation.LatestManifestUri,
            handler.RequestedUris);
    }

    [Fact]
    public async Task InvalidFallbackManifestIsRejectedWithoutAnotherPath()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        JsonObject manifest = UpdateTestData.CreateManifest();
        manifest["repository"] = "attacker/project";
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.Serialize(manifest)));
        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckForUpdateAsync(new Version(1, 0, 0)));

        Assert.Equal(2, handler.RequestedUris.Count);
    }

    private static LauncherUpdateService CreateService(
        QueueHttpMessageHandler handler,
        TemporaryUpdateDirectory directory) =>
        new(new HttpClient(handler), directory.Path);
}
