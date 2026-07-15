using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.Services;

/// <summary>
/// Routes each canonical platform to its own catalog and authentication provider.
/// </summary>
internal sealed class PlatformAdapterRegistry : IServerDirectory, IGameAuthenticationService
{
    private readonly IReadOnlyDictionary<string, PlatformAdapter> _adapters;

    public PlatformAdapterRegistry(IEnumerable<PlatformAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        var ordered = adapters.ToArray();
        if (ordered.Length == 0)
        {
            throw new ArgumentException("At least one platform adapter is required.", nameof(adapters));
        }

        var byId = new Dictionary<string, PlatformAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (PlatformAdapter adapter in ordered)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            if (!byId.TryAdd(adapter.Platform.Id, adapter))
            {
                throw new ArgumentException(
                    $"A platform adapter is already registered for '{adapter.Platform.Id}'.",
                    nameof(adapters));
            }
        }

        _adapters = byId;
        Platforms = Array.AsReadOnly(ordered.Select(static adapter => adapter.Platform).ToArray());
    }

    public IReadOnlyList<PlatformDefinition> Platforms { get; }

    public Task<ServerCatalog> GetServersAsync(
        PlatformDefinition platform,
        long userId = 0,
        CancellationToken cancellationToken = default)
    {
        PlatformAdapter adapter = ResolveCanonical(platform);
        return adapter.ServerDirectory.GetServersAsync(platform, userId, cancellationToken);
    }

    public Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PlatformAdapter adapter;
        try
        {
            adapter = ResolveCanonical(request.Platform);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(AuthenticationResult.Failure(
                "unsupported_platform",
                "A versão selecionada não corresponde a um provedor reconhecido."));
        }

        return adapter.AuthenticationService.AuthenticateAsync(request, cancellationToken);
    }

    private PlatformAdapter ResolveCanonical(PlatformDefinition platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        if (!_adapters.TryGetValue(platform.Id, out PlatformAdapter? adapter) ||
            adapter.Platform != platform)
        {
            throw new ArgumentException(
                "The platform definition is not registered or was modified.",
                nameof(platform));
        }

        return adapter;
    }
}

internal sealed class PlatformAdapter
{
    public PlatformAdapter(
        PlatformDefinition platform,
        IServerDirectory serverDirectory,
        IGameAuthenticationService authenticationService)
    {
        Platform = platform ?? throw new ArgumentNullException(nameof(platform));
        ServerDirectory = serverDirectory ?? throw new ArgumentNullException(nameof(serverDirectory));
        AuthenticationService = authenticationService ??
            throw new ArgumentNullException(nameof(authenticationService));
        ArgumentException.ThrowIfNullOrWhiteSpace(platform.Id);
    }

    public PlatformDefinition Platform { get; }

    public IServerDirectory ServerDirectory { get; }

    public IGameAuthenticationService AuthenticationService { get; }
}

internal sealed class UnavailablePlatformAuthenticationService(
    string errorCode,
    string errorMessage) : IGameAuthenticationService
{
    public Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AuthenticationResult.Failure(errorCode, errorMessage));
    }
}
