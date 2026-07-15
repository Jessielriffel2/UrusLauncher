namespace LegendLauncher.Infrastructure.Security;

/// <summary>
/// Creates and validates Credential Manager target names owned by the new launcher.
/// The namespace boundary deliberately excludes every legacy-client credential.
/// </summary>
public static class CredentialKey
{
    public const string Prefix = "LegendLauncherNext/";
    public const int MaximumLength = 32767;

    public static string ForProfile(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("A profile credential requires a non-empty identifier.", nameof(profileId));
        }

        return $"{Prefix}Profile/{profileId:N}";
    }

    public static void Validate(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (key.Length > MaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(key), "The credential key is too long.");
        }

        if (!key.StartsWith(Prefix, StringComparison.Ordinal) || key.Length == Prefix.Length)
        {
            throw new ArgumentException(
                $"Credential keys must belong to the '{Prefix}' namespace.",
                nameof(key));
        }

        foreach (var character in key)
        {
            if (char.IsControl(character))
            {
                throw new ArgumentException("Credential keys cannot contain control characters.", nameof(key));
            }
        }
    }
}
