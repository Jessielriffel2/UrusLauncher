namespace LegendLauncher.Tests.Infrastructure;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "LegendLauncherNext.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] segments) =>
        segments.Aggregate(Path, System.IO.Path.Combine);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A failed cleanup must not hide the test result. The OS temp area can reclaim it.
        }
        catch (UnauthorizedAccessException)
        {
            // Antivirus scanners can briefly hold test files on Windows.
        }
    }
}
