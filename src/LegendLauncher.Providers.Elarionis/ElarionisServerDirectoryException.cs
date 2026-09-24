namespace LegendLauncher.Providers.Elarionis;

/// <summary>
/// Public contextual error for Elarionis catalog failures.
/// </summary>
public sealed class ElarionisServerDirectoryException : Exception
{
    public ElarionisServerDirectoryException(string message)
        : base(message)
    {
    }

    public ElarionisServerDirectoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
