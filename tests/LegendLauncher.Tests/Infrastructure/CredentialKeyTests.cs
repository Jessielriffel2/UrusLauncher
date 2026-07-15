using LegendLauncher.Infrastructure.Security;

namespace LegendLauncher.Tests.Infrastructure;

public sealed class CredentialKeyTests
{
    [Fact]
    public void ForProfile_CreatesAValidatedLauncherOwnedKey()
    {
        var profileId = Guid.NewGuid();

        var key = CredentialKey.ForProfile(profileId);

        CredentialKey.Validate(key);
        Assert.Equal($"{CredentialKey.Prefix}Profile/{profileId:N}", key);
    }

    [Theory]
    [InlineData("LegacyClient/account")]
    [InlineData("LegendLauncherNext/")]
    [InlineData("LegendLauncherNext/Profile/abc\n")]
    public void Validate_RejectsKeysOutsideTheNewLauncherNamespace(string key)
    {
        Assert.ThrowsAny<ArgumentException>(() => CredentialKey.Validate(key));
    }

    [Fact]
    public void ForProfile_RejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(() => CredentialKey.ForProfile(Guid.Empty));
    }
}
