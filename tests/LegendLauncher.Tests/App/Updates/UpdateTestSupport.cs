using LegendLauncher.App.Updates;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LegendLauncher.Tests.App.Updates;

internal static class UpdateTestData
{
    public static byte[] InstallerBytes { get; } =
        "verified installer payload"u8.ToArray();

    public static string InstallerSha256(byte[]? bytes = null) =>
        Convert.ToHexString(SHA256.HashData(bytes ?? InstallerBytes)).ToLowerInvariant();

    public static JsonObject CreateManifest(
        string version = "1.2.3",
        byte[]? installerBytes = null)
    {
        byte[] payload = installerBytes ?? InstallerBytes;
        return new JsonObject
        {
            ["schema"] = 1,
            ["repository"] = LauncherUpdateValidation.Repository,
            ["version"] = version,
            ["installer"] = new JsonObject
            {
                ["name"] = $"UrusLauncher-Setup-{version}-win-x64.exe",
                ["bytes"] = payload.LongLength,
                ["sha256"] = InstallerSha256(payload),
            },
            ["notes"] = new JsonObject
            {
                ["pt-BR"] = "Notas em português",
                ["en-US"] = "English notes",
                ["es-ES"] = "Notas en español",
            },
        };
    }

    public static JsonObject CreateRelease(
        JsonObject manifest,
        string tagName = "v1.2.3",
        byte[]? installerBytes = null,
        bool includeDigests = true)
    {
        byte[] payload = installerBytes ?? InstallerBytes;
        byte[] manifestBytes = Serialize(manifest);
        string version = manifest["version"]!.GetValue<string>();
        string installerName = manifest["installer"]!["name"]!.GetValue<string>();
        string installerHash = manifest["installer"]!["sha256"]!.GetValue<string>();
        return new JsonObject
        {
            ["tag_name"] = tagName,
            ["assets"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = LauncherUpdateValidation.ManifestAssetName,
                    ["browser_download_url"] =
                        $"https://github.com/{LauncherUpdateValidation.Repository}/releases/download/{tagName}/{LauncherUpdateValidation.ManifestAssetName}",
                    ["size"] = manifestBytes.LongLength,
                    ["digest"] = includeDigests
                        ? $"sha256:{Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant()}"
                        : null,
                },
                new JsonObject
                {
                    ["name"] = installerName,
                    ["browser_download_url"] =
                        $"https://github.com/{LauncherUpdateValidation.Repository}/releases/download/{tagName}/{installerName}",
                    ["size"] = payload.LongLength,
                    ["digest"] = includeDigests ? $"sha256:{installerHash}" : null,
                },
            },
        };
    }

    public static void EnqueueCheck(
        QueueHttpMessageHandler handler,
        JsonObject manifest,
        JsonObject? release = null)
    {
        JsonObject actualRelease = release ?? CreateRelease(manifest);
        handler.Enqueue(Response(Serialize(actualRelease)));
        handler.Enqueue(Response(Serialize(manifest)));
    }

    public static LauncherUpdateRelease CreateReleaseModel(
        string version = "1.2.3",
        byte[]? installerBytes = null)
    {
        byte[] payload = installerBytes ?? InstallerBytes;
        var parsedVersion = Version.Parse(version);
        string name = LauncherUpdateValidation.ExpectedInstallerName(parsedVersion);
        return new LauncherUpdateRelease(
            parsedVersion,
            $"v{version}",
            new Dictionary<string, string>
            {
                ["pt-BR"] = "Notas",
                ["en-US"] = "Notes",
                ["es-ES"] = "Notas",
            },
            new LauncherUpdateInstaller(
                name,
                payload.LongLength,
                InstallerSha256(payload),
                new Uri($"https://github.com/{LauncherUpdateValidation.Repository}/releases/download/v{version}/{name}")));
    }

    public static byte[] Serialize(JsonObject value) =>
        JsonSerializer.SerializeToUtf8Bytes(value);

    public static HttpResponseMessage Response(
        byte[] content,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(content),
        };
    }

    public static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) },
        };
}

internal sealed class QueueHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = [];

    public List<Uri> RequestedUris { get; } = [];

    public void Enqueue(HttpResponseMessage response) =>
        _responses.Enqueue((_, _) => Task.FromResult(response));

    public void Enqueue(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) =>
        _responses.Enqueue(responseFactory);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestedUris.Add(request.RequestUri!);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No HTTP response was queued for the update test.");
        }

        return _responses.Dequeue()(request, cancellationToken);
    }
}

internal sealed class RecordingUpdateProcessStarter : IUpdateProcessStarter
{
    public ProcessStartInfo? StartInfo { get; private set; }

    public int CallCount { get; private set; }

    public void Start(ProcessStartInfo startInfo)
    {
        CallCount++;
        StartInfo = startInfo;
    }
}

internal sealed class TemporaryUpdateDirectory : IDisposable
{
    public TemporaryUpdateDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "UrusLauncher.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

internal sealed class CancelAfterFirstReadStream(
    byte[] content,
    CancellationTokenSource cancellation) : Stream
{
    private int _position;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => content.LongLength;
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_position >= content.Length)
        {
            return ValueTask.FromResult(0);
        }

        int count = Math.Min(Math.Min(buffer.Length, 4), content.Length - _position);
        content.AsMemory(_position, count).CopyTo(buffer);
        _position += count;
        cancellation.Cancel();
        return ValueTask.FromResult(count);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class StallingReadStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return 0;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
