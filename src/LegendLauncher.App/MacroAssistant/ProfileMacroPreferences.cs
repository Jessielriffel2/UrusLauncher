namespace LegendLauncher.App.MacroAssistant;

internal sealed record MacroProfilePreferences(
    double TargetX,
    double TargetY,
    double AnalysisX,
    double AnalysisY,
    double AnalysisWidth,
    double AnalysisHeight,
    bool UseLargeFrame,
    double Speed,
    int ClickCount,
    double ClickIntervalSeconds)
{
    public static MacroProfilePreferences Default { get; } =
        new(
            0.5,
            0.5,
            0.25,
            0.325,
            0.5,
            0.35,
            true,
            1.0,
            0,
            0.10);

    public MacroProfilePreferences Normalized()
    {
        double targetX = Clamp01(TargetX);
        double targetY = Clamp01(TargetY);
        double width = Math.Clamp(AnalysisWidth, 0.08, 1.0);
        double height = Math.Clamp(AnalysisHeight, 0.08, 1.0);
        return this with
        {
            TargetX = targetX,
            TargetY = targetY,
            AnalysisX = Math.Clamp(AnalysisX, 0, 1 - width),
            AnalysisY = Math.Clamp(AnalysisY, 0, 1 - height),
            AnalysisWidth = width,
            AnalysisHeight = height,
            Speed = Math.Clamp(Speed, 0.25, 4.0),
            ClickCount = Math.Max(0, ClickCount),
            ClickIntervalSeconds = Math.Clamp(ClickIntervalSeconds, 0.01, 3600),
        };
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
}

internal sealed record ProfileMacroModes(
    Dictionary<string, MacroProfilePreferences> Modes)
{
    public static ProfileMacroModes Empty { get; } =
        new(new Dictionary<string, MacroProfilePreferences>());

    public MacroProfilePreferences Get(MacroMode mode) =>
        Modes is { } modes &&
        modes.TryGetValue(ProfileMacroPreferencesStoreKeys.ModeKey(mode), out MacroProfilePreferences? preferences)
            ? preferences.Normalized()
            : MacroProfilePreferences.Default;

    public ProfileMacroModes With(MacroMode mode, MacroProfilePreferences preferences)
    {
        var copy = new Dictionary<string, MacroProfilePreferences>(
            Modes ?? new Dictionary<string, MacroProfilePreferences>(),
            StringComparer.OrdinalIgnoreCase)
        {
            [ProfileMacroPreferencesStoreKeys.ModeKey(mode)] = preferences.Normalized(),
        };
        return new ProfileMacroModes(copy);
    }
}

internal static class ProfileMacroPreferencesStoreKeys
{
    public const string Gems = "Gems";
    public const string Cosmo = "Cosmo";
    public const string Clicks = "Clicks";

    public static string ModeKey(MacroMode mode) => mode switch
    {
        MacroMode.Cosmo => Cosmo,
        MacroMode.Clicks => Clicks,
        _ => Gems,
    };
}
