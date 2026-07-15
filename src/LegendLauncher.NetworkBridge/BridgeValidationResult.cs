namespace LegendLauncher.NetworkBridge;

public sealed record BridgeValidationResult(bool IsAllowed, string? Reason)
{
    public static BridgeValidationResult Allow() => new(true, null);

    public static BridgeValidationResult Deny(string reason) => new(false, reason);
}
