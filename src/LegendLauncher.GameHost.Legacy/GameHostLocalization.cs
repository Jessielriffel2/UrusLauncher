using System.Globalization;

namespace LegendLauncher.GameHost.Legacy;

internal enum GameHostText
{
    InvalidOptions,
    FlashRuntimeIncomplete,
    LocalSessionInvalid,
    CompatibilityProbe,
    FlashInitializationFailed,
    PreparingIsolatedEnvironment,
    IsolatedGameHost,
    DiagnosticModeDescription,
    CompatibilityFound,
    CompatibilityFoundDetail,
    CompatibilityIncomplete,
    MissingLabel,
    VerifiedFolderLabel,
    MissingFlashActiveXFromManifest,
}

internal static class GameHostLocalization
{
    internal const string EnvironmentVariableName = "LEGEND_LAUNCHER_LANGUAGE";
    internal const string DefaultCultureName = "pt-BR";

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<GameHostText, string>>
        Catalog = new Dictionary<string, IReadOnlyDictionary<GameHostText, string>>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["pt-BR"] = new Dictionary<GameHostText, string>
            {
                [GameHostText.InvalidOptions] =
                    "Não foi possível iniciar o GameHost porque os parâmetros recebidos são inválidos.",
                [GameHostText.FlashRuntimeIncomplete] =
                    "O runtime Flash local está incompleto. Execute o modo de diagnóstico.",
                [GameHostText.LocalSessionInvalid] =
                    "A sessão local não pôde ser validada. Tente iniciar o jogo novamente pelo launcher.",
                [GameHostText.CompatibilityProbe] = "Verificação de compatibilidade",
                [GameHostText.FlashInitializationFailed] =
                    "Não foi possível inicializar o runtime Flash isolado. Execute o diagnóstico e confira os componentes locais.",
                [GameHostText.PreparingIsolatedEnvironment] = "Preparando o ambiente isolado…",
                [GameHostText.IsolatedGameHost] = "GameHost isolado",
                [GameHostText.DiagnosticModeDescription] =
                    "Modo de diagnóstico: nenhum endereço, token ou senha foi recebido. O ActiveX não será iniciado nesta etapa.",
                [GameHostText.CompatibilityFound] = "Compatibilidade encontrada",
                [GameHostText.CompatibilityFoundDetail] =
                    "Manifesto e Flash ActiveX x64 localizados. O host está pronto para receber uma sessão pelo canal local protegido.",
                [GameHostText.CompatibilityIncomplete] = "Compatibilidade incompleta",
                [GameHostText.MissingLabel] = "Faltando",
                [GameHostText.VerifiedFolderLabel] = "Pasta verificada",
                [GameHostText.MissingFlashActiveXFromManifest] =
                    "Flash ActiveX referenciado pelo manifesto",
            },
            ["en-US"] = new Dictionary<GameHostText, string>
            {
                [GameHostText.InvalidOptions] =
                    "GameHost could not start because the received options are invalid.",
                [GameHostText.FlashRuntimeIncomplete] =
                    "The local Flash runtime is incomplete. Run diagnostics mode.",
                [GameHostText.LocalSessionInvalid] =
                    "The local session could not be validated. Start the game again from the launcher.",
                [GameHostText.CompatibilityProbe] = "Compatibility check",
                [GameHostText.FlashInitializationFailed] =
                    "The isolated Flash runtime could not be initialized. Run diagnostics and verify the local components.",
                [GameHostText.PreparingIsolatedEnvironment] = "Preparing the isolated environment…",
                [GameHostText.IsolatedGameHost] = "Isolated GameHost",
                [GameHostText.DiagnosticModeDescription] =
                    "Diagnostics mode: no address, token, or password was received. ActiveX will not start during this step.",
                [GameHostText.CompatibilityFound] = "Compatibility detected",
                [GameHostText.CompatibilityFoundDetail] =
                    "The manifest and local Flash ActiveX x64 were found. The host is ready to receive a session through the protected local channel.",
                [GameHostText.CompatibilityIncomplete] = "Compatibility incomplete",
                [GameHostText.MissingLabel] = "Missing",
                [GameHostText.VerifiedFolderLabel] = "Verified folder",
                [GameHostText.MissingFlashActiveXFromManifest] =
                    "Flash ActiveX referenced by the manifest",
            },
            ["es-ES"] = new Dictionary<GameHostText, string>
            {
                [GameHostText.InvalidOptions] =
                    "No se pudo iniciar GameHost porque los parámetros recibidos no son válidos.",
                [GameHostText.FlashRuntimeIncomplete] =
                    "El entorno local de Flash está incompleto. Ejecute el modo de diagnóstico.",
                [GameHostText.LocalSessionInvalid] =
                    "No se pudo validar la sesión local. Intente iniciar el juego nuevamente desde el launcher.",
                [GameHostText.CompatibilityProbe] = "Comprobación de compatibilidad",
                [GameHostText.FlashInitializationFailed] =
                    "No se pudo inicializar el entorno aislado de Flash. Ejecute el diagnóstico y compruebe los componentes locales.",
                [GameHostText.PreparingIsolatedEnvironment] = "Preparando el entorno aislado…",
                [GameHostText.IsolatedGameHost] = "GameHost aislado",
                [GameHostText.DiagnosticModeDescription] =
                    "Modo de diagnóstico: no se recibió ninguna dirección, token ni contraseña. ActiveX no se iniciará en esta etapa.",
                [GameHostText.CompatibilityFound] = "Compatibilidad detectada",
                [GameHostText.CompatibilityFoundDetail] =
                    "Se localizaron el manifiesto y Flash ActiveX x64. El host está listo para recibir una sesión mediante el canal local protegido.",
                [GameHostText.CompatibilityIncomplete] = "Compatibilidad incompleta",
                [GameHostText.MissingLabel] = "Faltan",
                [GameHostText.VerifiedFolderLabel] = "Carpeta verificada",
                [GameHostText.MissingFlashActiveXFromManifest] =
                    "Flash ActiveX al que hace referencia el manifiesto",
            },
        };

    internal static IReadOnlyList<string> SupportedCultureNames { get; } =
        ["pt-BR", "en-US", "es-ES"];

    internal static string NormalizeCultureName(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return DefaultCultureName;
        }

        string normalized = cultureName.Trim().Replace('_', '-');
        if (normalized.Equals("pt", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("pt-", StringComparison.OrdinalIgnoreCase))
        {
            return "pt-BR";
        }

        if (normalized.Equals("en", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
        {
            return "en-US";
        }

        if (normalized.Equals("es", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("es-", StringComparison.OrdinalIgnoreCase))
        {
            return "es-ES";
        }

        return DefaultCultureName;
    }

    internal static CultureInfo InitializeFromEnvironment()
    {
        string? configured = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        string requested = string.IsNullOrWhiteSpace(configured)
            ? CultureInfo.CurrentUICulture.Name
            : configured;
        var culture = new CultureInfo(NormalizeCultureName(requested));
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        return culture;
    }

    internal static string Get(GameHostText text) =>
        Get(text, CultureInfo.CurrentUICulture.Name);

    internal static string Get(GameHostText text, string? cultureName)
    {
        string normalized = NormalizeCultureName(cultureName);
        return Catalog[normalized][text];
    }
}
