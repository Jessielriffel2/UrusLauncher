using LegendLauncher.App.Updates;
using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace LegendLauncher.Tests.App.Updates;

public sealed class LauncherUpdateServiceDownloadTests
{
    [Fact]
    public async Task DownloadStreamsVerifiesAndAtomicallyRenamesInstaller()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.InstallerBytes));
        var starter = new RecordingUpdateProcessStarter();
        var service = CreateService(handler, directory, starter);
        LauncherUpdateRelease release = UpdateTestData.CreateReleaseModel();
        var progressValues = new List<double>();

        DownloadedLauncherInstaller installer = await service.DownloadInstallerAsync(
            release,
            new ImmediateProgress(progressValues.Add));

        Assert.Equal(Path.Combine(directory.Path, release.Installer.Name), installer.FilePath);
        Assert.Equal(UpdateTestData.InstallerBytes, await File.ReadAllBytesAsync(installer.FilePath));
        Assert.False(File.Exists(installer.FilePath + ".part"));
        Assert.Equal(0, progressValues[0]);
        Assert.Equal(1, progressValues[^1]);
        Assert.All(progressValues, value => Assert.InRange(value, 0, 1));
        Assert.Equal(0, starter.CallCount);
    }

    [Fact]
    public async Task DownloadFollowsAllowedRedirect()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue(UpdateTestData.Redirect("https://objects.githubusercontent.com/release-object"));
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.InstallerBytes));
        var service = CreateService(handler, directory);

        DownloadedLauncherInstaller installer = await service.DownloadInstallerAsync(
            UpdateTestData.CreateReleaseModel());

        Assert.True(File.Exists(installer.FilePath));
        Assert.Equal(2, handler.RequestedUris.Count);
    }

    [Fact]
    public async Task WrongHashDeletesPartialFileAndDoesNotPublishInstaller()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.InstallerBytes));
        LauncherUpdateRelease valid = UpdateTestData.CreateReleaseModel();
        LauncherUpdateRelease release = valid with
        {
            Installer = valid.Installer with { Sha256 = new string('0', 64) },
        };
        var service = CreateService(handler, directory);
        string finalPath = Path.Combine(directory.Path, release.Installer.Name);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadInstallerAsync(release));

        Assert.False(File.Exists(finalPath));
        Assert.False(File.Exists(finalPath + ".part"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task WrongResponseSizeDeletesPartialFile(long sizeDifference)
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        byte[] responseBytes = sizeDifference < 0
            ? UpdateTestData.InstallerBytes[..^1]
            : [.. UpdateTestData.InstallerBytes, 0];
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(responseBytes),
        };
        handler.Enqueue(response);
        LauncherUpdateRelease release = UpdateTestData.CreateReleaseModel();
        var service = CreateService(handler, directory);
        string finalPath = Path.Combine(directory.Path, release.Installer.Name);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadInstallerAsync(release));

        Assert.False(File.Exists(finalPath));
        Assert.False(File.Exists(finalPath + ".part"));
    }

    [Fact]
    public async Task CancellationDuringStreamingDeletesPartialFile()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var cancellation = new CancellationTokenSource();
        using var handler = new QueueHttpMessageHandler();
        var stream = new CancelAfterFirstReadStream(
            UpdateTestData.InstallerBytes,
            cancellation);
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream),
        });
        LauncherUpdateRelease release = UpdateTestData.CreateReleaseModel();
        var service = CreateService(handler, directory);
        string finalPath = Path.Combine(directory.Path, release.Installer.Name);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DownloadInstallerAsync(release, cancellationToken: cancellation.Token));

        Assert.False(File.Exists(finalPath));
        Assert.False(File.Exists(finalPath + ".part"));
    }

    [Fact]
    public async Task DownloadTimeoutDeletesPartialFileAndBecomesHttpFailure()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new StallingReadStream()),
        });
        LauncherUpdateRelease release = UpdateTestData.CreateReleaseModel();
        var service = new LauncherUpdateService(
            new HttpClient(handler),
            directory.Path,
            downloadTimeout: TimeSpan.FromMilliseconds(20));
        string finalPath = Path.Combine(directory.Path, release.Installer.Name);

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.DownloadInstallerAsync(release));

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(finalPath));
        Assert.False(File.Exists(finalPath + ".part"));
    }

    [Fact]
    public async Task InvalidDownloadUrlIsRejectedBeforeNetworkAccess()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        LauncherUpdateRelease valid = UpdateTestData.CreateReleaseModel();
        LauncherUpdateRelease release = valid with
        {
            Installer = valid.Installer with
            {
                DownloadUri = new Uri("https://evil.example/update.exe"),
            },
        };
        var service = CreateService(handler, directory);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadInstallerAsync(release));

        Assert.Empty(handler.RequestedUris);
    }

    [Fact]
    public async Task LaunchReverifiesFileAndUsesSilentUpdateArguments()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.InstallerBytes));
        var starter = new RecordingUpdateProcessStarter();
        var service = CreateService(handler, directory, starter);
        DownloadedLauncherInstaller installer = await service.DownloadInstallerAsync(
            UpdateTestData.CreateReleaseModel());

        await service.LaunchInstallerAsync(installer);

        Assert.Equal(1, starter.CallCount);
        ProcessStartInfo startInfo = Assert.IsType<ProcessStartInfo>(starter.StartInfo);
        Assert.Equal(installer.FilePath, startInfo.FileName);
        Assert.Equal(directory.Path, startInfo.WorkingDirectory);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal(
            [
                "/SP-",
                "/SILENT",
                "/SUPPRESSMSGBOXES",
                "/NORESTART",
                "/CLOSEAPPLICATIONS",
                "/RELAUNCH",
            ],
            startInfo.ArgumentList);
    }

    [Fact]
    public async Task TamperedInstallerIsNeverStarted()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        handler.Enqueue(UpdateTestData.Response(UpdateTestData.InstallerBytes));
        var starter = new RecordingUpdateProcessStarter();
        var service = CreateService(handler, directory, starter);
        DownloadedLauncherInstaller installer = await service.DownloadInstallerAsync(
            UpdateTestData.CreateReleaseModel());
        await File.WriteAllBytesAsync(installer.FilePath, new byte[installer.Bytes]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.LaunchInstallerAsync(installer));

        Assert.Equal(0, starter.CallCount);
    }

    [Fact]
    public async Task InstallerOutsideDownloadDirectoryIsNeverStarted()
    {
        using var directory = new TemporaryUpdateDirectory();
        using var outside = new TemporaryUpdateDirectory();
        using var handler = new QueueHttpMessageHandler();
        var starter = new RecordingUpdateProcessStarter();
        var service = CreateService(handler, directory, starter);
        LauncherUpdateRelease release = UpdateTestData.CreateReleaseModel();
        string outsidePath = Path.Combine(outside.Path, release.Installer.Name);
        await File.WriteAllBytesAsync(outsidePath, UpdateTestData.InstallerBytes);
        var installer = new DownloadedLauncherInstaller(
            outsidePath,
            release.Installer.Name,
            release.Installer.Bytes,
            release.Installer.Sha256);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.LaunchInstallerAsync(installer));

        Assert.Equal(0, starter.CallCount);
    }

    private static LauncherUpdateService CreateService(
        QueueHttpMessageHandler handler,
        TemporaryUpdateDirectory directory,
        IUpdateProcessStarter? starter = null) =>
        new(new HttpClient(handler), directory.Path, starter);

    private sealed class ImmediateProgress(Action<double> onReport) : IProgress<double>
    {
        public void Report(double value) => onReport(value);
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }
}
