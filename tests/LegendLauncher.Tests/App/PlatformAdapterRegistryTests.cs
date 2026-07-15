using LegendLauncher.App.Services;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Tests.App;

public sealed class PlatformAdapterRegistryTests
{
    private static readonly PlatformDefinition First = new(
        "first",
        "First",
        "first-code",
        new Uri("https://first.example/catalog"),
        "pt-BR");
    private static readonly PlatformDefinition Second = new(
        "second",
        "Second",
        "second-code",
        new Uri("https://second.example/catalog"),
        "en-US");

    [Fact]
    public async Task Registry_RoutesCatalogAndAuthenticationToMatchingAdapterOnly()
    {
        var firstDirectory = DirectoryFor(First);
        var secondDirectory = DirectoryFor(Second);
        var firstAuthentication = RejectingAuthentication("first-rejected");
        var secondAuthentication = RejectingAuthentication("second-rejected");
        var registry = new PlatformAdapterRegistry(
        [
            new PlatformAdapter(First, firstDirectory, firstAuthentication),
            new PlatformAdapter(Second, secondDirectory, secondAuthentication),
        ]);

        ServerCatalog catalog = await registry.GetServersAsync(Second, userId: 42);
        AuthenticationResult authentication = await registry.AuthenticateAsync(RequestFor(Second));

        Assert.Equal(Second.Id, catalog.PlatformId);
        Assert.Empty(firstDirectory.Requests);
        Assert.Single(secondDirectory.Requests);
        Assert.Equal((Second.Id, 42), secondDirectory.Requests[0]);
        Assert.Empty(firstAuthentication.Requests);
        Assert.Single(secondAuthentication.Requests);
        Assert.Equal("second-rejected", authentication.ErrorCode);
    }

    [Fact]
    public async Task Registry_RejectsTamperedCanonicalDefinitionBeforeProviderCall()
    {
        var directory = DirectoryFor(First);
        var authentication = RejectingAuthentication("unused");
        var registry = new PlatformAdapterRegistry(
            [new PlatformAdapter(First, directory, authentication)]);
        PlatformDefinition tampered = First with { GameCode = "changed" };

        await Assert.ThrowsAsync<ArgumentException>(() => registry.GetServersAsync(tampered));
        AuthenticationResult result = await registry.AuthenticateAsync(RequestFor(tampered));

        Assert.Empty(directory.Requests);
        Assert.Empty(authentication.Requests);
        Assert.Equal("unsupported_platform", result.ErrorCode);
    }

    [Fact]
    public void Registry_RejectsDuplicatePlatformIdsCaseInsensitively()
    {
        PlatformDefinition duplicate = Second with { Id = "FIRST" };

        Assert.Throws<ArgumentException>(() => new PlatformAdapterRegistry(
        [
            new PlatformAdapter(First, DirectoryFor(First), RejectingAuthentication("one")),
            new PlatformAdapter(duplicate, DirectoryFor(duplicate), RejectingAuthentication("two")),
        ]));
    }

    [Fact]
    public async Task UnavailableAuthentication_ReturnsExplicitFailureWithoutSession()
    {
        var service = new UnavailablePlatformAuthenticationService(
            "catalog_only",
            "Catalog only");

        AuthenticationResult result = await service.AuthenticateAsync(RequestFor(First));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Equal("catalog_only", result.ErrorCode);
        Assert.Equal("Catalog only", result.ErrorMessage);
    }

    private static StubServerDirectory DirectoryFor(PlatformDefinition platform) => new(
        (requested, _, _) => Task.FromResult(new ServerCatalog(
            requested.Id,
            [new GameServer("1", requested.DisplayName)],
            [],
            null,
            AppTestData.Now)));

    private static StubAuthenticationService RejectingAuthentication(string errorCode) => new(
        (_, _) => Task.FromResult(AuthenticationResult.Failure(errorCode)));

    private static AuthenticationRequest RequestFor(PlatformDefinition platform) => new(
        platform,
        new GameServer("1", "Server"),
        "user@example.com",
        new CredentialSecret("user@example.com", "secret"));
}
