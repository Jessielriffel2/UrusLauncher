using System.Xml;

namespace LegendLauncher.Infrastructure.Runtime;

/// <summary>
/// Locates the existing legacy runtime without registering components, starting
/// processes, changing permissions or writing into the legacy installation.
/// </summary>
public sealed class LegacyRuntimeProbe
{
    private const string ManifestFileName = "Adobe.Flash.Control.manifest";

    public LegacyRuntimeProbeResult Probe(
        string? configuredPath = null,
        string? startDirectory = null)
    {
        var candidates = EnumerateCandidates(configuredPath, startDirectory).ToArray();
        CandidateProbe? bestPartial = null;

        foreach (var candidate in candidates)
        {
            var result = ProbeCandidate(candidate);
            if (result.IsUsable)
            {
                return result.ToResult();
            }

            if (result.FoundComponentCount > (bestPartial?.FoundComponentCount ?? 0))
            {
                bestPartial = result;
            }
        }

        return bestPartial?.ToResult() ?? new LegacyRuntimeProbeResult(
            IsUsable: false,
            RuntimeDirectory: null,
            ManifestPath: null,
            FlashOcxPath: null,
            MissingComponents:
            [
                LegacyRuntimeComponent.FlashManifest,
                LegacyRuntimeComponent.FlashActiveXControl
            ],
            Source: LegacyRuntimeProbeSource.NotFound);
    }

    private static IEnumerable<RuntimeCandidate> EnumerateCandidates(
        string? configuredPath,
        string? startDirectory)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var configuredDirectory = TryNormalizeDirectory(configuredPath);
        if (configuredDirectory is not null)
        {
            var isFirst = true;
            foreach (var directory in EnumerateSelfAndAncestors(configuredDirectory))
            {
                if (visited.Add(directory))
                {
                    yield return new RuntimeCandidate(
                        directory,
                        isFirst
                            ? LegacyRuntimeProbeSource.ConfiguredPath
                            : LegacyRuntimeProbeSource.AncestorSearch);
                }

                isFirst = false;
            }
        }

        var searchStart = TryNormalizeDirectory(startDirectory) ??
            TryNormalizeDirectory(AppContext.BaseDirectory);

        if (searchStart is null)
        {
            yield break;
        }

        foreach (var directory in EnumerateSelfAndAncestors(searchStart))
        {
            if (visited.Add(directory))
            {
                yield return new RuntimeCandidate(
                    directory,
                    LegacyRuntimeProbeSource.AncestorSearch);
            }
        }
    }

    private static IEnumerable<string> EnumerateSelfAndAncestors(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            yield return current.FullName;
        }
    }

    private static string? TryNormalizeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path.Trim().Trim('"'));
            if (File.Exists(fullPath))
            {
                return Path.GetDirectoryName(fullPath);
            }

            return fullPath;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static CandidateProbe ProbeCandidate(RuntimeCandidate candidate)
    {
        var manifestPath = ExistingFile(Path.Combine(candidate.Directory, ManifestFileName));
        var ocxPath = FindFlashOcx(candidate.Directory, manifestPath);

        return new CandidateProbe(candidate, manifestPath, ocxPath);
    }

    private static string? FindFlashOcx(string runtimeDirectory, string? manifestPath)
    {
        if (manifestPath is not null)
        {
            return FindManifestFlashOcx(runtimeDirectory, manifestPath);
        }

        var flashDirectory = Path.Combine(runtimeDirectory, "flash");
        var inFlashDirectory = FirstOcxFile(flashDirectory);
        return inFlashDirectory ?? FirstOcxFile(runtimeDirectory);
    }

    private static string? FindManifestFlashOcx(string runtimeDirectory, string manifestPath)
    {
        try
        {
            using var stream = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 1024 * 1024
                });

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element ||
                    !reader.LocalName.Equals("file", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relativePath = reader.GetAttribute("name");
                if (string.IsNullOrWhiteSpace(relativePath) ||
                    !relativePath.EndsWith(".ocx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var candidatePath = Path.GetFullPath(Path.Combine(runtimeDirectory, relativePath));
                if (IsWithinDirectory(runtimeDirectory, candidatePath) && File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            return null;
        }
        catch (Exception exception) when (
            exception is XmlException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool IsWithinDirectory(string directory, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(directory, candidatePath);
        return !Path.IsPathRooted(relativePath) &&
               relativePath != ".." &&
               !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string? FirstOcxFile(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return null;
            }

            return Directory
                .EnumerateFiles(directory, "*.ocx", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ExistingFile(string path)
    {
        try
        {
            return File.Exists(path) ? Path.GetFullPath(path) : null;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed record RuntimeCandidate(
        string Directory,
        LegacyRuntimeProbeSource Source);

    private sealed record CandidateProbe(
        RuntimeCandidate Candidate,
        string? ManifestPath,
        string? FlashOcxPath)
    {
        public bool IsUsable => FoundComponentCount == 2;

        public int FoundComponentCount =>
            (ManifestPath is null ? 0 : 1) +
            (FlashOcxPath is null ? 0 : 1);

        public LegacyRuntimeProbeResult ToResult()
        {
            var missing = new List<LegacyRuntimeComponent>(capacity: 2);
            if (ManifestPath is null)
            {
                missing.Add(LegacyRuntimeComponent.FlashManifest);
            }

            if (FlashOcxPath is null)
            {
                missing.Add(LegacyRuntimeComponent.FlashActiveXControl);
            }

            return new LegacyRuntimeProbeResult(
                IsUsable,
                Candidate.Directory,
                ManifestPath,
                FlashOcxPath,
                missing,
                Candidate.Source);
        }
    }
}
