namespace LegendLauncher.Infrastructure.Paths;

/// <summary>
/// Defines the writable directories owned exclusively by Urus Launcher.
/// </summary>
public sealed class AppPaths
{
    public const string DefaultApplicationDirectoryName = "LegendLauncherNext";
    public const string UserLogDirectoryName = "uruslauncher";

    /// <summary>
    /// Creates paths below the current user's LocalApplicationData directory.
    /// </summary>
    public AppPaths()
        : this(
            GetLocalApplicationDataDirectory(),
            GetDocumentsDirectory(),
            DefaultApplicationDirectoryName)
    {
    }

    /// <summary>
    /// Creates paths below a supplied LocalApplicationData base directory.
    /// This overload exists so callers and tests never need to change process-wide environment variables.
    /// </summary>
    public AppPaths(string localApplicationDataDirectory)
        : this(
            localApplicationDataDirectory,
            GetDocumentsDirectory(),
            DefaultApplicationDirectoryName)
    {
    }

    public AppPaths(string localApplicationDataDirectory, string applicationDirectoryName)
        : this(
            localApplicationDataDirectory,
            GetDocumentsDirectory(),
            applicationDirectoryName)
    {
    }

    public AppPaths(
        string localApplicationDataDirectory,
        string documentsDirectory,
        string applicationDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentsDirectory);
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
        ProfilePreferencesFile = Path.Combine(DataDirectory, "profile-preferences.json");
        ProfileAvatarsDirectory = Path.Combine(DataDirectory, "avatars");
        SettingsFile = Path.Combine(DataDirectory, "settings.json");
        LogsDirectory = Path.GetFullPath(
            Path.Combine(documentsDirectory, UserLogDirectoryName, "logs"));
    }

    public string RootDirectory { get; }

    public string CacheDirectory { get; }

    public string DataDirectory { get; }

    public string UpdatesDirectory { get; }

    public string CatalogCacheFile { get; }

    public string ProfilesFile { get; }

    public string ProfilePreferencesFile { get; }

    public string ProfileAvatarsDirectory { get; }

    public string SettingsFile { get; }

    /// <summary>
    /// Gets the user-visible diagnostic log directory under Documents/uruslauncher/logs.
    /// </summary>
    public string LogsDirectory { get; }

    /// <summary>
    /// Creates only the directories owned by this application.
    /// </summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ProfileAvatarsDirectory);
        Directory.CreateDirectory(UpdatesDirectory);
    }

    private static string GetDocumentsDirectory()
    {
        string directory = Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile,
                Environment.SpecialFolderOption.DoNotVerify);
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("A documents directory is not available for the current user.");
        }

        return directory;
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
