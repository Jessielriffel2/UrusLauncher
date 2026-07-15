using System.IO;
using System.Security;

namespace LegendLauncher.App.Updates;

internal static class UpdateDownloadCleanup
{
    internal static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    public static void DeleteStaleArtifacts(
        string downloadDirectory,
        DateTimeOffset utcNow)
    {
        string[] files;
        try
        {
            if ((File.GetAttributes(downloadDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                return;
            }

            files = Directory.GetFiles(
                downloadDirectory,
                "*",
                SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception) when (IsSafeFileSystemFailure(exception))
        {
            return;
        }

        DateTime cutoffUtc = utcNow.UtcDateTime - MinimumAge;
        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            if (!IsOfficialArtifact(fileName))
            {
                continue;
            }

            try
            {
                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
                if (lastWriteUtc >= cutoffUtc)
                {
                    continue;
                }

                File.Delete(filePath);
            }
            catch (Exception exception) when (IsSafeFileSystemFailure(exception))
            {
                // A locked or inaccessible artifact must never prevent startup.
            }
        }
    }

    internal static bool IsOfficialArtifact(string fileName)
    {
        if (LauncherUpdateValidation.IsInstallerName(fileName))
        {
            return true;
        }

        const string partialSuffix = ".part";
        return fileName.EndsWith(partialSuffix, StringComparison.Ordinal) &&
            LauncherUpdateValidation.IsInstallerName(fileName[..^partialSuffix.Length]);
    }

    private static bool IsSafeFileSystemFailure(Exception exception) =>
        exception is IOException or
            UnauthorizedAccessException or
            SecurityException or
            NotSupportedException;
}
