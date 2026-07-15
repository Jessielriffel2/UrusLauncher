namespace LegendLauncher.App.Localization;

internal readonly record struct LocalizedMessage(
    string Key,
    object?[] Arguments)
{
    public static LocalizedMessage Create(string key, params object?[] arguments) =>
        new(key, arguments);

    public string Resolve(LocalizationService localization) =>
        localization.Format(Key, Arguments);
}
