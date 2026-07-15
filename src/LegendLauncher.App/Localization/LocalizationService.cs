using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace LegendLauncher.App.Localization;

internal sealed record LanguageOption(
    string Code,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}

internal sealed class LocalizationService : INotifyPropertyChanged
{
    internal const string DefaultLanguageCode = "pt-BR";
    internal const string EnglishLanguageCode = "en-US";
    internal const string SpanishLanguageCode = "es-ES";

    private static readonly IReadOnlyList<LanguageOption> LanguageOptions = Array.AsReadOnly(
    [
        new LanguageOption(DefaultLanguageCode, "Português (Brasil)"),
        new LanguageOption(EnglishLanguageCode, "English"),
        new LanguageOption(SpanishLanguageCode, "Español"),
    ]);

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalogs =
        LoadCatalogs();
    private bool _updatesThreadCulture;
    private string _languageCode;
    private CultureInfo _culture;

    internal LocalizationService(string? initialLanguageCode = null)
    {
        _languageCode = NormalizeLanguageCode(initialLanguageCode);
        _culture = CultureInfo.GetCultureInfo(_languageCode);
    }

    public static LocalizationService Current { get; } = new();

    public static IReadOnlyList<LanguageOption> SupportedLanguages => LanguageOptions;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public string LanguageCode => _languageCode;

    public CultureInfo Culture => _culture;

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (Catalogs[_languageCode].TryGetValue(key, out string? value))
        {
            return value;
        }

        return Catalogs[DefaultLanguageCode].TryGetValue(key, out string? fallback)
            ? fallback
            : $"[{key}]";
    }

    public string Format(string key, params object?[] arguments) =>
        string.Format(_culture, Get(key), arguments);

    public bool SetLanguage(string? languageCode)
    {
        string normalized = NormalizeLanguageCode(languageCode);
        if (string.Equals(_languageCode, normalized, StringComparison.Ordinal))
        {
            if (_updatesThreadCulture)
            {
                ApplyThreadCulture();
            }

            return false;
        }

        _languageCode = normalized;
        _culture = CultureInfo.GetCultureInfo(normalized);
        if (_updatesThreadCulture)
        {
            ApplyThreadCulture();
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageCode)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    internal void EnableThreadCultureUpdates()
    {
        _updatesThreadCulture = true;
        ApplyThreadCulture();
    }

    internal static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return DefaultLanguageCode;
        }

        string candidate = languageCode.Trim().Replace('_', '-');
        if (candidate.Equals("pt", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("pt-", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultLanguageCode;
        }

        if (candidate.Equals("en", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
        {
            return EnglishLanguageCode;
        }

        if (candidate.Equals("es", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("es-", StringComparison.OrdinalIgnoreCase))
        {
            return SpanishLanguageCode;
        }

        return DefaultLanguageCode;
    }

    internal static IReadOnlyDictionary<string, string> GetCatalog(string languageCode) =>
        Catalogs[NormalizeLanguageCode(languageCode)];

    private void ApplyThreadCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadCatalogs()
    {
        string[] codes = [DefaultLanguageCode, EnglishLanguageCode, SpanishLanguageCode];
        var catalogs = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        Assembly assembly = typeof(LocalizationService).Assembly;
        string assemblyName = assembly.GetName().Name ?? "LegendLauncher.App";
        foreach (string code in codes)
        {
            string resourceName = $"{assemblyName}.Localization.Resources.{code}.json";
            using Stream stream = assembly.GetManifestResourceStream(resourceName) ??
                throw new InvalidOperationException($"The localization resource '{resourceName}' is missing.");
            Dictionary<string, string>? values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            if (values is null || values.Count == 0 || values.Any(static pair =>
                    string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
            {
                throw new InvalidDataException($"The localization resource '{resourceName}' is invalid.");
            }

            catalogs.Add(code, new Dictionary<string, string>(values, StringComparer.Ordinal));
        }

        return catalogs;
    }
}
