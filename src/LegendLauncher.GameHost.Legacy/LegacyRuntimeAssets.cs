using System.Xml;

namespace LegendLauncher.GameHost.Legacy;

internal sealed record LegacyRuntimeAssets(
    string RuntimeRoot,
    string ManifestPath,
    string? FlashOcxPath,
    bool IsComplete,
    IReadOnlyList<string> MissingFiles)
{
    private const string ManifestFileName = "Adobe.Flash.Control.manifest";

    public static LegacyRuntimeAssets Discover(string runtimeRoot)
    {
        string fullRoot = Path.GetFullPath(runtimeRoot);
        string manifest = Path.Combine(fullRoot, ManifestFileName);
        var missing = new List<string>(2);
        string? flashOcx = null;

        if (!File.Exists(manifest))
        {
            missing.Add(ManifestFileName);
        }
        else
        {
            flashOcx = FindManifestFlashOcx(fullRoot, manifest);
        }

        if (flashOcx is null)
        {
            missing.Add(GameHostLocalization.Get(GameHostText.MissingFlashActiveXFromManifest));
        }

        return new LegacyRuntimeAssets(
            fullRoot,
            manifest,
            flashOcx,
            missing.Count == 0,
            missing);
    }

    private static string? FindManifestFlashOcx(string runtimeRoot, string manifestPath)
    {
        try
        {
            using var stream = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 1024 * 1024,
            });

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element ||
                    !reader.LocalName.Equals("file", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? relativePath = reader.GetAttribute("name");
                if (string.IsNullOrWhiteSpace(relativePath) ||
                    Path.IsPathRooted(relativePath) ||
                    !relativePath.EndsWith(".ocx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = Path.GetFullPath(Path.Combine(runtimeRoot, relativePath));
                if (IsWithinRuntime(runtimeRoot, candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch (Exception exception) when (
            exception is XmlException or IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            return null;
        }

        return null;
    }

    private static bool IsWithinRuntime(string runtimeRoot, string candidate)
    {
        string relative = Path.GetRelativePath(runtimeRoot, candidate);
        return !Path.IsPathRooted(relative) &&
               relative != ".." &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
