namespace LegendLauncher.Infrastructure.Paths;

/// <summary>
/// Defines the writable directories owned exclusively by Urus Launcher.
/// </summary>
public sealed class AppPaths
{
    public const string DefaultApplicationDirectoryName = "LegendLauncherNext";

    /// <summary>
    /// Creates paths below the current user's LocalApplicationData directory.
    /// </summary>
    public AppPaths()
        : this(GetLocalApplicationDataDirectory(), DefaultApplicationDirectoryName)
    {
    }

    /// <summary>
    /// Creates paths below a supplied LocalApplicationData base directory.
    /// This overload exists so callers and tests never need to change process-wide environment variables.
    /// </summary>
    public AppPaths(string localApplicationDataDirectory)
        : this(localApplicationDataDirectory, DefaultApplicationDirectoryName)
    {
    }

    public AppPaths(string localApplicationDataDirectory, string applicationDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectoryName);

        if (applicationDirectoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            applicationDirectoryName is "." or "..")
        {
            throw new ArgumentException("The application directory name is invalid.", nameof(applicationDirectoryName));
        }

        RootDirectory = Path.GetFullPath(
            Path.Combine(localApplicationDataDirectory, applicationDirectoryName));
        CacheDirectory = Path.Combine(RootDirectory, "cache");
        DataDirectory = Path.Combine(RootDirectory, "data");
        UpdatesDirectory = Path.Combine(RootDirectory, "updates");
        CatalogCacheFile = Path.Combine(CacheDirectory, "server-catalogs.json");
        ProfilesFile = Path.Combine(DataDirectory, "profiles.json");
        SettingsFile = Path.Combine(DataDirectory, "settings.json");
    }

    public string RootDirectory { get; }

    public string CacheDirectory { get; }

    public string DataDirectory { get; }

    public string UpdatesDirectory { get; }

    public string CatalogCacheFile { get; }

    public string ProfilesFile { get; }

    public string SettingsFile { get; }

    /// <summary>
    /// Creates only the directories owned by this application.
    /// </summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(UpdatesDirectory);
    }

    private static string GetLocalApplicationDataDirectory()
    {
        var directory = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("LocalApplicationData is not available for the current user.");
        }

        return directory;
    }
}
