using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace LegendLauncher.App.Updates;

internal sealed class LauncherUpdateService : ILauncherUpdateService
{
    private const int MaximumRedirects = 5;
    private readonly HttpClient _httpClient;
    private readonly string _downloadDirectory;
    private readonly IUpdateProcessStarter _processStarter;
    private readonly TimeSpan _checkTimeout;
    private readonly TimeSpan _downloadTimeout;

    public LauncherUpdateService(
        HttpClient httpClient,
        string downloadDirectory,
        IUpdateProcessStarter? processStarter = null,
        TimeSpan? checkTimeout = null,
        TimeSpan? downloadTimeout = null,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadDirectory);
        _downloadDirectory = Path.GetFullPath(downloadDirectory);
        _processStarter = processStarter ?? new UpdateProcessStarter();
        _checkTimeout = ValidateTimeout(checkTimeout ?? TimeSpan.FromSeconds(15), nameof(checkTimeout));
        _downloadTimeout = ValidateTimeout(
            downloadTimeout ?? TimeSpan.FromMinutes(5),
            nameof(downloadTimeout));
        UpdateDownloadCleanup.DeleteStaleArtifacts(
            _downloadDirectory,
            (timeProvider ?? TimeProvider.System).GetUtcNow());
    }

    public async Task<LauncherUpdateRelease?> CheckForUpdateAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeout = CreateTimeoutTokenSource(
            cancellationToken,
            _checkTimeout);
        try
        {
            return await CheckForUpdateCoreAsync(currentVersion, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new HttpRequestException("The update check timed out.", exception);
        }
    }

    private async Task<LauncherUpdateRelease?> CheckForUpdateCoreAsync(
        Version currentVersion,
        CancellationToken cancellationToken)
    {
        Version normalizedCurrentVersion =
            LauncherUpdateValidation.NormalizeCurrentVersion(currentVersion);
        GitHubReleaseDocument githubRelease;
        try
        {
            githubRelease = await ReadJsonAsync<GitHubReleaseDocument>(
                LauncherUpdateValidation.LatestReleaseUri,
                LauncherUpdateValidation.MaximumJsonBytes,
                expectedBytes: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (IsPublicApiRateLimit(exception))
        {
            LauncherUpdateRelease fallbackRelease =
                await ReadFallbackReleaseAsync(cancellationToken).ConfigureAwait(false);
            return fallbackRelease.Version > normalizedCurrentVersion
                ? fallbackRelease
                : null;
        }

        string tagName = githubRelease.TagName
            ?? throw new InvalidDataException("The GitHub release does not contain a tag.");
        Version tagVersion = LauncherUpdateValidation.ParseTag(tagName);
        GitHubReleaseAssetDocument manifestAsset = FindSingleAsset(
            githubRelease.Assets,
            LauncherUpdateValidation.ManifestAssetName);
        ValidateReportedAssetSize(
            manifestAsset,
            LauncherUpdateValidation.MaximumJsonBytes);
        Uri manifestUri = LauncherUpdateValidation.ParseReleaseAssetUri(
            manifestAsset.BrowserDownloadUrl,
            tagName,
            LauncherUpdateValidation.ManifestAssetName);

        byte[] manifestBytes = await ReadBytesAsync(
            manifestUri,
            LauncherUpdateValidation.MaximumJsonBytes,
            manifestAsset.Size,
            cancellationToken).ConfigureAwait(false);
        ValidateReportedDigest(manifestAsset, manifestBytes);
        UpdateManifestDocument manifest = Deserialize<UpdateManifestDocument>(
            manifestBytes,
            "The update manifest is not valid JSON.");
        LauncherUpdateRelease release = BuildRelease(
            tagName,
            tagVersion,
            githubRelease.Assets,
            manifest);

        return release.Version > normalizedCurrentVersion ? release : null;
    }

    private async Task<LauncherUpdateRelease> ReadFallbackReleaseAsync(
        CancellationToken cancellationToken)
    {
        byte[] manifestBytes = await ReadBytesAsync(
            LauncherUpdateValidation.LatestManifestUri,
            LauncherUpdateValidation.MaximumJsonBytes,
            expectedBytes: null,
            cancellationToken).ConfigureAwait(false);
        UpdateManifestDocument manifest = Deserialize<UpdateManifestDocument>(
            manifestBytes,
            "The update manifest is not valid JSON.");
        return BuildFallbackRelease(manifest);
    }

    public async Task<DownloadedLauncherInstaller> DownloadInstallerAsync(
        LauncherUpdateRelease release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeout = CreateTimeoutTokenSource(
            cancellationToken,
            _downloadTimeout);
        try
        {
            return await DownloadInstallerCoreAsync(release, progress, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new HttpRequestException("The update download timed out.", exception);
        }
    }

    private async Task<DownloadedLauncherInstaller> DownloadInstallerCoreAsync(
        LauncherUpdateRelease release,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ValidateReleaseForDownload(release);
        Directory.CreateDirectory(_downloadDirectory);

        string finalPath = Path.Combine(_downloadDirectory, release.Installer.Name);
        string partialPath = finalPath + ".part";
        DeleteIfExists(partialPath);
        progress?.Report(0);

        try
        {
            using HttpResponseMessage response = await SendWithRedirectsAsync(
                release.Installer.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            ValidateResponseLength(response, release.Installer.Bytes);

            string actualSha256;
            long totalBytes = 0;
            await using (var destination = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
            await using (Stream source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[81920];
                while (true)
                {
                    int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    totalBytes = checked(totalBytes + read);
                    if (totalBytes > release.Installer.Bytes ||
                        totalBytes > LauncherUpdateValidation.MaximumInstallerBytes)
                    {
                        throw new InvalidDataException("The downloaded installer exceeds its declared size.");
                    }

                    hash.AppendData(buffer, 0, read);
                    await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken).ConfigureAwait(false);
                    progress?.Report((double)totalBytes / release.Installer.Bytes);
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                actualSha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            if (totalBytes != release.Installer.Bytes)
            {
                throw new InvalidDataException("The downloaded installer size does not match the manifest.");
            }

            if (!LauncherUpdateValidation.FixedTimeSha256Equals(
                release.Installer.Sha256,
                actualSha256))
            {
                throw new InvalidDataException("The downloaded installer failed SHA-256 verification.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partialPath, finalPath, overwrite: true);
            progress?.Report(1);
            return new DownloadedLauncherInstaller(
                finalPath,
                release.Installer.Name,
                release.Installer.Bytes,
                release.Installer.Sha256);
        }
        catch
        {
            DeleteIfExists(partialPath);
            throw;
        }
    }

    public async Task LaunchInstallerAsync(
        DownloadedLauncherInstaller installer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installer);
        string fullPath = ValidateDownloadedInstallerPath(installer);
        await using FileStream lockedInstaller = OpenInstallerForVerification(fullPath);
        await VerifyDownloadedInstallerAsync(
            lockedInstaller,
            installer.Bytes,
            installer.Sha256,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = fullPath,
            WorkingDirectory = _downloadDirectory,
            UseShellExecute = true,
        };
        foreach (string argument in new[]
        {
            "/SP-",
            "/SILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/CLOSEAPPLICATIONS",
            "/RELAUNCH",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        _processStarter.Start(startInfo);
    }

    private static LauncherUpdateRelease BuildRelease(
        string tagName,
        Version tagVersion,
        GitHubReleaseAssetDocument[]? githubAssets,
        UpdateManifestDocument manifest)
    {
        ValidatedUpdateManifest validated = UpdateManifestValidator.Validate(manifest, tagVersion);
        GitHubReleaseAssetDocument githubInstaller = FindSingleAsset(
            githubAssets,
            validated.InstallerName);
        ValidateInstallerAssetMetadata(githubInstaller, validated);
        Uri installerUri = LauncherUpdateValidation.ParseReleaseAssetUri(
            githubInstaller.BrowserDownloadUrl,
            tagName,
            validated.InstallerName);

        return CreateRelease(tagName, validated, installerUri);
    }

    private static LauncherUpdateRelease BuildFallbackRelease(UpdateManifestDocument manifest)
    {
        ValidatedUpdateManifest validated = UpdateManifestValidator.Validate(
            manifest,
            expectedVersion: null);
        string tagName = $"v{validated.Version.ToString(3)}";
        var installerUri = new Uri(
            $"https://github.com/{LauncherUpdateValidation.Repository}/releases/download/{tagName}/{validated.InstallerName}");
        _ = LauncherUpdateValidation.ParseReleaseAssetUri(
            installerUri.AbsoluteUri,
            tagName,
            validated.InstallerName);

        return CreateRelease(tagName, validated, installerUri);
    }

    private static LauncherUpdateRelease CreateRelease(
        string tagName,
        ValidatedUpdateManifest manifest,
        Uri installerUri)
    {
        return new LauncherUpdateRelease(
            manifest.Version,
            tagName,
            manifest.LocalizedNotes,
            new LauncherUpdateInstaller(
                manifest.InstallerName,
                manifest.InstallerBytes,
                manifest.InstallerSha256,
                installerUri));
    }

    private static void ValidateInstallerAssetMetadata(
        GitHubReleaseAssetDocument githubAsset,
        ValidatedUpdateManifest installer)
    {
        if (githubAsset.Size is long reportedSize && reportedSize != installer.InstallerBytes)
        {
            throw new InvalidDataException("The GitHub installer size does not match the update manifest.");
        }

        string? reportedDigest = LauncherUpdateValidation.ParseGitHubDigest(
            githubAsset.Digest,
            installer.InstallerName);
        if (reportedDigest is not null &&
            !LauncherUpdateValidation.FixedTimeSha256Equals(installer.InstallerSha256, reportedDigest))
        {
            throw new InvalidDataException("The GitHub installer digest does not match the update manifest.");
        }
    }

    private static void ValidateReleaseForDownload(LauncherUpdateRelease release)
    {
        string expectedTag = $"v{release.Version.ToString(3)}";
        if (!string.Equals(release.TagName, expectedTag, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The selected update tag is invalid.");
        }

        string expectedName = LauncherUpdateValidation.ExpectedInstallerName(release.Version);
        if (!string.Equals(release.Installer.Name, expectedName, StringComparison.Ordinal) ||
            release.Installer.Bytes <= 0 ||
            release.Installer.Bytes > LauncherUpdateValidation.MaximumInstallerBytes)
        {
            throw new InvalidDataException("The selected update installer metadata is invalid.");
        }

        LauncherUpdateValidation.ValidateSha256(release.Installer.Sha256, "The installer hash");
        _ = LauncherUpdateValidation.ParseReleaseAssetUri(
            release.Installer.DownloadUri.AbsoluteUri,
            release.TagName,
            release.Installer.Name);
    }

    private string ValidateDownloadedInstallerPath(DownloadedLauncherInstaller installer)
    {
        if (!string.Equals(installer.Name, Path.GetFileName(installer.Name), StringComparison.Ordinal) ||
            !LauncherUpdateValidation.IsInstallerName(installer.Name) ||
            installer.Bytes <= 0 ||
            installer.Bytes > LauncherUpdateValidation.MaximumInstallerBytes)
        {
            throw new InvalidDataException("The downloaded installer metadata is invalid.");
        }

        LauncherUpdateValidation.ValidateSha256(installer.Sha256, "The downloaded installer hash");
        string expectedPath = Path.GetFullPath(Path.Combine(_downloadDirectory, installer.Name));
        string actualPath = Path.GetFullPath(installer.FilePath);
        if (!string.Equals(expectedPath, actualPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The downloaded installer is outside the update directory.");
        }

        return actualPath;
    }

    private static FileStream OpenInstallerForVerification(string filePath) =>
        new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static async Task VerifyDownloadedInstallerAsync(
        FileStream stream,
        long expectedBytes,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        if (stream.Length != expectedBytes)
        {
            throw new InvalidDataException("The installer changed after it was downloaded.");
        }

        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        string actualSha256 = Convert.ToHexString(digest).ToLowerInvariant();
        if (!LauncherUpdateValidation.FixedTimeSha256Equals(expectedSha256, actualSha256))
        {
            throw new InvalidDataException("The installer changed after it was downloaded.");
        }
    }

    private async Task<T> ReadJsonAsync<T>(
        Uri uri,
        long maximumBytes,
        long? expectedBytes,
        CancellationToken cancellationToken)
    {
        byte[] content = await ReadBytesAsync(
            uri,
            maximumBytes,
            expectedBytes,
            cancellationToken).ConfigureAwait(false);
        return Deserialize<T>(content, "The GitHub update response is not valid JSON.");
    }

    private async Task<byte[]> ReadBytesAsync(
        Uri uri,
        long maximumBytes,
        long? expectedBytes,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendWithRedirectsAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength is long contentLength &&
            (contentLength < 0 || contentLength > maximumBytes ||
             expectedBytes is long expected && contentLength != expected))
        {
            throw new InvalidDataException("The update response size is invalid.");
        }

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[32768];
        while (true)
        {
            int read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > maximumBytes)
            {
                throw new InvalidDataException("The update response exceeds the allowed size.");
            }

            buffer.Write(chunk, 0, read);
        }

        if (expectedBytes is long exactBytes && buffer.Length != exactBytes)
        {
            throw new InvalidDataException("The update response size does not match GitHub metadata.");
        }

        return buffer.ToArray();
    }

    private async Task<HttpResponseMessage> SendWithRedirectsAsync(
        Uri initialUri,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        Uri currentUri = initialUri;
        for (int redirects = 0; ; redirects++)
        {
            LauncherUpdateValidation.ValidateAllowedUri(currentUri);
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.UserAgent.ParseAdd("UrusLauncher-Updater/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                completionOption,
                cancellationToken).ConfigureAwait(false);

            if (!IsRedirect(response.StatusCode))
            {
                if (!response.IsSuccessStatusCode)
                {
                    HttpStatusCode responseStatus = response.StatusCode;
                    int statusCode = (int)responseStatus;
                    response.Dispose();
                    throw new HttpRequestException(
                        $"The update request returned HTTP {statusCode}.",
                        null,
                        responseStatus);
                }

                return response;
            }

            Uri? location = response.Headers.Location;
            response.Dispose();
            if (location is null)
            {
                throw new HttpRequestException("The update redirect did not contain a destination.");
            }

            if (redirects >= MaximumRedirects)
            {
                throw new HttpRequestException("The update request exceeded the redirect limit.");
            }

            currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
        }
    }

    private static void ValidateResponseLength(HttpResponseMessage response, long expectedBytes)
    {
        if (response.Content.Headers.ContentLength is long contentLength &&
            contentLength != expectedBytes)
        {
            throw new InvalidDataException("The installer Content-Length does not match the manifest.");
        }
    }

    private static GitHubReleaseAssetDocument FindSingleAsset(
        GitHubReleaseAssetDocument[]? assets,
        string name)
    {
        GitHubReleaseAssetDocument[] matches = (assets ?? [])
            .Where(asset => string.Equals(asset.Name, name, StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidDataException($"The GitHub release is missing {name}."),
            _ => throw new InvalidDataException($"The GitHub release contains duplicate {name} assets."),
        };
    }

    private static void ValidateReportedAssetSize(
        GitHubReleaseAssetDocument asset,
        long maximumBytes)
    {
        if (asset.Size is long size && (size <= 0 || size > maximumBytes))
        {
            throw new InvalidDataException($"The GitHub size for {asset.Name} is invalid.");
        }
    }

    private static void ValidateReportedDigest(
        GitHubReleaseAssetDocument asset,
        byte[] content)
    {
        string? expected = LauncherUpdateValidation.ParseGitHubDigest(asset.Digest, asset.Name!);
        if (expected is null)
        {
            return;
        }

        string actual = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!LauncherUpdateValidation.FixedTimeSha256Equals(expected, actual))
        {
            throw new InvalidDataException($"The GitHub digest for {asset.Name} is invalid.");
        }
    }

    private static T Deserialize<T>(byte[] content, string errorMessage)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content)
                ?? throw new InvalidDataException(errorMessage);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(errorMessage, exception);
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or
            HttpStatusCode.Found or
            HttpStatusCode.SeeOther or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;

    private static bool IsPublicApiRateLimit(HttpRequestException exception) =>
        exception.StatusCode is HttpStatusCode.Forbidden or
            HttpStatusCode.TooManyRequests;

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static TimeSpan ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The update timeout must be positive and supported by the system timer.");
        }

        return timeout;
    }

    private static CancellationTokenSource CreateTimeoutTokenSource(
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        CancellationTokenSource source =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }
}
