using System.Collections.ObjectModel;
using System.IO;

namespace LegendLauncher.App.Updates;

internal static class UpdateManifestValidator
{
    private static readonly string[] RequiredLanguages = ["pt-BR", "en-US", "es-ES"];

    public static ValidatedUpdateManifest Validate(
        UpdateManifestDocument manifest,
        Version? expectedVersion)
    {
        if (manifest.Schema != 1)
        {
            throw new InvalidDataException("The update manifest schema is not supported.");
        }

        if (!string.Equals(
            manifest.Repository,
            LauncherUpdateValidation.Repository,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("The update manifest repository does not match the launcher repository.");
        }

        Version manifestVersion = LauncherUpdateValidation.ParseManifestVersion(manifest.Version);
        if (expectedVersion is not null && manifestVersion != expectedVersion)
        {
            throw new InvalidDataException("The update manifest version does not match the GitHub release tag.");
        }

        UpdateManifestInstallerDocument installer = manifest.Installer
            ?? throw new InvalidDataException("The update manifest does not contain an installer.");
        string expectedInstallerName = LauncherUpdateValidation.ExpectedInstallerName(manifestVersion);
        if (!string.Equals(installer.Name, expectedInstallerName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The installer asset name does not match the release version.");
        }

        if (installer.Bytes <= 0 ||
            installer.Bytes > LauncherUpdateValidation.MaximumInstallerBytes)
        {
            throw new InvalidDataException("The installer size in the manifest is invalid.");
        }

        LauncherUpdateValidation.ValidateSha256(installer.Sha256, "The installer hash");
        return new ValidatedUpdateManifest(
            manifestVersion,
            expectedInstallerName,
            installer.Bytes,
            installer.Sha256!.ToLowerInvariant(),
            ValidateNotes(manifest.Notes));
    }

    private static IReadOnlyDictionary<string, string> ValidateNotes(
        Dictionary<string, string?>? notes)
    {
        if (notes is null || notes.Count != RequiredLanguages.Length)
        {
            throw new InvalidDataException("The update manifest must contain notes in all supported languages.");
        }

        var validated = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string language in RequiredLanguages)
        {
            if (!notes.TryGetValue(language, out string? value) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"The update notes for {language} are missing.");
            }

            validated.Add(language, value.Trim());
        }

        return new ReadOnlyDictionary<string, string>(validated);
    }
}

internal sealed record ValidatedUpdateManifest(
    Version Version,
    string InstallerName,
    long InstallerBytes,
    string InstallerSha256,
    IReadOnlyDictionary<string, string> LocalizedNotes);
