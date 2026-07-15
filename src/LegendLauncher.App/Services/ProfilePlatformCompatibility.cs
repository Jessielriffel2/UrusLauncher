namespace LegendLauncher.App.Services;

internal static class ProfilePlatformCompatibility
{
    private const string OasPlatformPrefix = "oas-";

    public static bool ShareAccountIdentity(string firstPlatformId, string secondPlatformId)
    {
        if (string.IsNullOrWhiteSpace(firstPlatformId) ||
            string.IsNullOrWhiteSpace(secondPlatformId))
        {
            return false;
        }

        if (string.Equals(
                firstPlatformId.Trim(),
                secondPlatformId.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsOasPlatform(firstPlatformId) && IsOasPlatform(secondPlatformId);
    }

    private static bool IsOasPlatform(string platformId) =>
        platformId.Trim().StartsWith(OasPlatformPrefix, StringComparison.OrdinalIgnoreCase);
}
