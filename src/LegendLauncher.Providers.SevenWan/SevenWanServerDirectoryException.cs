namespace LegendLauncher.Providers.SevenWan;

public sealed class SevenWanServerDirectoryException : Exception
{
    public SevenWanServerDirectoryException(string message)
        : base(message)
    {
    }

    public SevenWanServerDirectoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
