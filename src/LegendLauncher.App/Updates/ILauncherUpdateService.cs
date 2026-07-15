namespace LegendLauncher.App.Updates;

internal interface ILauncherUpdateService
{
    Task<LauncherUpdateRelease?> CheckForUpdateAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default);

    Task<DownloadedLauncherInstaller> DownloadInstallerAsync(
        LauncherUpdateRelease release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task LaunchInstallerAsync(
        DownloadedLauncherInstaller installer,
        CancellationToken cancellationToken = default);
}

internal sealed record LauncherUpdateRelease(
    Version Version,
    string TagName,
    IReadOnlyDictionary<string, string> LocalizedNotes,
    LauncherUpdateInstaller Installer)
{
    public string GetNotes(string languageCode)
    {
        if (LocalizedNotes.TryGetValue(languageCode, out string? exact))
        {
            return exact;
        }

        return LocalizedNotes.TryGetValue("pt-BR", out string? fallback)
            ? fallback
            : string.Empty;
    }
}

internal sealed record LauncherUpdateInstaller(
    string Name,
    long Bytes,
    string Sha256,
    Uri DownloadUri);

internal sealed record DownloadedLauncherInstaller(
    string FilePath,
    string Name,
    long Bytes,
    string Sha256);
