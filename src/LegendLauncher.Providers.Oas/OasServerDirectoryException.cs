namespace LegendLauncher.Providers.Oas;

public sealed class OasServerDirectoryException : Exception
{
    public OasServerDirectoryException(string message)
        : base(message)
    {
    }

    public OasServerDirectoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
