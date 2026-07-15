using System.Diagnostics;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Security;

namespace LegendLauncher.App.Services;

internal sealed class SessionLaunchCoordinator
{
    private const int MaximumRecentServerCount = 5;

    private readonly ICredentialVault _credentialVault;
    private readonly IGameAuthenticationService _authenticationService;
    private readonly IGameRuntime _gameRuntime;
    private readonly IProfileStore _profileStore;
    private readonly GameRuntimeOptions? _runtimeOptions;
    private readonly Action<int> _terminateUnadoptedProcess;
    private readonly TimeProvider _timeProvider;

    public SessionLaunchCoordinator(
        ICredentialVault credentialVault,
        IGameAuthenticationService authenticationService,
        IGameRuntime gameRuntime,
        string? runtimeRoot,
        IProfileStore profileStore,
        TimeProvider? timeProvider = null,
        Action<int>? terminateUnadoptedProcess = null)
    {
        _credentialVault = credentialVault ?? throw new ArgumentNullException(nameof(credentialVault));
        _authenticationService = authenticationService ??
            throw new ArgumentNullException(nameof(authenticationService));
        _gameRuntime = gameRuntime ?? throw new ArgumentNullException(nameof(gameRuntime));
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _runtimeOptions = string.IsNullOrWhiteSpace(runtimeRoot)
            ? null
            : new GameRuntimeOptions(runtimeRoot);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _terminateUnadoptedProcess = terminateUnadoptedProcess ?? TryTerminateProcess;
    }

    public async Task<SessionLaunchOutcome> LaunchAsync(
        SessionLaunchInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        if (_runtimeOptions is null)
        {
            throw new InvalidOperationException("The game compatibility runtime is unavailable.");
        }

        ResolvedCredential? credential = await ResolveCredentialAsync(input, cancellationToken)
            .ConfigureAwait(false);
        if (credential is null)
        {
            return SessionLaunchOutcome.CredentialRequired();
        }

        var authenticationRequest = new AuthenticationRequest(
            input.Platform,
            input.Server,
            input.Login,
            credential.Secret);
        AuthenticationResult authentication = await _authenticationService
            .AuthenticateAsync(authenticationRequest, cancellationToken)
            .ConfigureAwait(false);
        if (!authentication.IsSuccess || authentication.Session is null)
        {
            return SessionLaunchOutcome.AuthenticationRejected(
                authentication.ErrorCode,
                authentication.ErrorMessage,
                credential.Source);
        }

        GameSession gameSession = await _gameRuntime
            .LaunchAsync(authentication.Session, _runtimeOptions, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            (AccountProfile effectiveProfile, bool wasProfilePersisted, bool wasCredentialPersisted) =
                await TryPersistSuccessfulLaunchAsync(input, authentication, cancellationToken)
                    .ConfigureAwait(false);

            return SessionLaunchOutcome.Success(
                gameSession,
                effectiveProfile,
                wasProfilePersisted,
                wasCredentialPersisted,
                credential.Source);
        }
        catch
        {
            _terminateUnadoptedProcess(gameSession.ProcessId);
            throw;
        }
    }

    private async Task<ResolvedCredential?> ResolveCredentialAsync(
        SessionLaunchInput input,
        CancellationToken cancellationToken)
    {
        if (input.TypedPassword.Length > 0)
        {
            return new ResolvedCredential(
                new CredentialSecret(input.Login, input.TypedPassword),
                SessionCredentialSource.Typed);
        }

        if (input.Profile is null ||
            !ProfilePlatformCompatibility.ShareAccountIdentity(
                input.Profile.PlatformId,
                input.Platform.Id) ||
            !string.Equals(input.Profile.UserName, input.Login, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        CredentialSecret? stored = await _credentialVault
            .GetAsync(input.Profile.CredentialKey, cancellationToken)
            .ConfigureAwait(false);
        if (stored is null ||
            !string.Equals(stored.UserName, input.Login, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new ResolvedCredential(
            new CredentialSecret(input.Login, stored.Password),
            SessionCredentialSource.Stored);
    }

    private async Task<(
        AccountProfile Profile,
        bool WasProfilePersisted,
        bool WasCredentialPersisted)> TryPersistSuccessfulLaunchAsync(
        SessionLaunchInput input,
        AuthenticationResult authentication,
        CancellationToken cancellationToken)
    {
        bool reusesExistingProfile = input.Profile is not null &&
            ProfilePlatformCompatibility.ShareAccountIdentity(
                input.Profile.PlatformId,
                input.Platform.Id) &&
            string.Equals(input.Profile.UserName, input.Login, StringComparison.OrdinalIgnoreCase);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        IReadOnlyList<string> recentServerIds = BuildRecentServerIds(
            reusesExistingProfile
                ? input.Profile!.GetRecentServerIds(input.Platform.Id)
                : [],
            input.Server.Id);
        AccountProfile effectiveProfile = reusesExistingProfile
            ? input.Profile!.WithPlatformLaunchState(
                input.Platform.Id,
                authentication.ProviderUserId ?? input.Profile.GetProviderUserId(input.Platform.Id),
                recentServerIds,
                now)
            : CreateProfile(input, authentication, recentServerIds, now);
        if (!reusesExistingProfile && !input.RememberPassword)
        {
            return (effectiveProfile, false, true);
        }

        try
        {
            await _profileStore.SaveAsync(effectiveProfile, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return (effectiveProfile, false, false);
        }

        try
        {
            if (input.RememberPassword && input.TypedPassword.Length > 0)
            {
                await _credentialVault
                    .SetAsync(
                        effectiveProfile.CredentialKey,
                        new CredentialSecret(input.Login, input.TypedPassword),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!input.RememberPassword)
            {
                await _credentialVault
                    .DeleteAsync(effectiveProfile.CredentialKey, cancellationToken)
                    .ConfigureAwait(false);
            }

            return (effectiveProfile, true, true);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return (effectiveProfile, true, false);
        }
    }

    private static AccountProfile CreateProfile(
        SessionLaunchInput input,
        AuthenticationResult authentication,
        IReadOnlyList<string> recentServerIds,
        DateTimeOffset now)
    {
        Guid profileId = Guid.NewGuid();
        var profile = new AccountProfile(
            profileId,
            string.IsNullOrWhiteSpace(input.ProfileDisplayName)
                ? input.Login
                : input.ProfileDisplayName.Trim(),
            input.Platform.Id,
            input.Login,
            CredentialKey.ForProfile(profileId),
            authentication.ProviderUserId,
            input.Server.Id,
            now,
            now)
        {
            RecentServerIds = recentServerIds,
        };
        return profile.WithPlatformLaunchState(
            input.Platform.Id,
            authentication.ProviderUserId,
            recentServerIds,
            now);
    }

    internal static void TryTerminateProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
            System.ComponentModel.Win32Exception)
        {
            // The GameHost already exited or Windows refused the best-effort cleanup.
        }
    }

    private static IReadOnlyList<string> BuildRecentServerIds(
        IEnumerable<string> previousServerIds,
        string currentServerId) =>
        previousServerIds
            .Prepend(currentServerId)
            .Where(static serverId => !string.IsNullOrWhiteSpace(serverId))
            .Select(static serverId => serverId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumRecentServerCount)
            .ToArray();

    private sealed record ResolvedCredential(
        CredentialSecret Secret,
        SessionCredentialSource Source);
}

internal sealed class SessionLaunchInput
{
    public SessionLaunchInput(
        AccountProfile? profile,
        PlatformDefinition platform,
        GameServer server,
        string profileDisplayName,
        string login,
        string typedPassword,
        bool rememberPassword)
    {
        Profile = profile;
        Platform = platform ?? throw new ArgumentNullException(nameof(platform));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        ProfileDisplayName = profileDisplayName ?? throw new ArgumentNullException(nameof(profileDisplayName));
        Login = login ?? throw new ArgumentNullException(nameof(login));
        TypedPassword = typedPassword ?? throw new ArgumentNullException(nameof(typedPassword));
        RememberPassword = rememberPassword;
    }

    public AccountProfile? Profile { get; }

    public PlatformDefinition Platform { get; }

    public GameServer Server { get; }

    public string ProfileDisplayName { get; }

    public string Login { get; }

    public string TypedPassword { get; }

    public bool RememberPassword { get; }

    public override string ToString() =>
        $"SessionLaunchInput {{ HasProfile = {Profile is not null}, Platform = {Platform.Id}, Server = {Server.Id}, HasProfileDisplayName = {ProfileDisplayName.Length > 0}, HasLogin = {Login.Length > 0}, HasPassword = {TypedPassword.Length > 0}, RememberPassword = {RememberPassword} }}";
}

internal enum SessionLaunchState
{
    Success,
    CredentialRequired,
    AuthenticationRejected,
}

internal enum SessionCredentialSource
{
    None,
    Typed,
    Stored,
}

internal sealed record SessionLaunchOutcome(
    SessionLaunchState State,
    SessionCredentialSource CredentialSource,
    GameSession? GameSession,
    AccountProfile? EffectiveProfile,
    bool WasProfilePersisted,
    bool WasCredentialPersisted,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static SessionLaunchOutcome Success(
        GameSession gameSession,
        AccountProfile effectiveProfile,
        bool wasProfilePersisted,
        bool wasCredentialPersisted,
        SessionCredentialSource credentialSource) =>
        new(
            SessionLaunchState.Success,
            credentialSource,
            gameSession ?? throw new ArgumentNullException(nameof(gameSession)),
            effectiveProfile ?? throw new ArgumentNullException(nameof(effectiveProfile)),
            wasProfilePersisted,
            wasCredentialPersisted,
            null,
            null);

    public static SessionLaunchOutcome CredentialRequired() =>
        new(
            SessionLaunchState.CredentialRequired,
            SessionCredentialSource.None,
            null,
            null,
            false,
            false,
            null,
            null);

    public static SessionLaunchOutcome AuthenticationRejected(
        string? errorCode,
        string? errorMessage,
        SessionCredentialSource credentialSource) =>
        new(
            SessionLaunchState.AuthenticationRejected,
            credentialSource,
            null,
            null,
            false,
            false,
            errorCode,
            errorMessage);

    public override string ToString() =>
        $"SessionLaunchOutcome {{ State = {State}, CredentialSource = {CredentialSource}, HasGameSession = {GameSession is not null}, HasEffectiveProfile = {EffectiveProfile is not null}, WasProfilePersisted = {WasProfilePersisted}, WasCredentialPersisted = {WasCredentialPersisted}, HasErrorCode = {ErrorCode is not null}, HasErrorMessage = {ErrorMessage is not null} }}";
}
