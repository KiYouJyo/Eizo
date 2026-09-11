using System.Diagnostics;
using Eizo.Metadata.Recognition;

namespace Eizo.Recognition;

public sealed record MediaRecognitionRuntimeInfo(
    string Version,
    string AssemblyPath,
    string ProbeStatus);

public sealed class MediaRecognitionService
{
    private readonly IRecognitionEngine _engine;

    public MediaRecognitionService()
        : this(new RecognitionEngine())
    {
    }

    internal MediaRecognitionService(IRecognitionEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public static string RuntimeVersion => GetRuntimeIdentity().Version;

    public static string RuntimeAssemblyPath => GetRuntimeIdentity().AssemblyPath;

    public static MediaRecognitionRuntimeInfo ProbeRuntime()
    {
        var service = new MediaRecognitionService();
        var snapshot = service.Recognize("Eizo.Runtime.Probe.S01E01.mkv");
        if (snapshot.Status == MediaRecognitionStatus.Error)
            throw new InvalidOperationException(
                $"Recognition runtime probe failed: {snapshot.ErrorCode ?? "Unknown"}");

        var identity = GetRuntimeIdentity();
        return new(identity.Version, identity.AssemblyPath, snapshot.Status.ToString());
    }

    public MediaRecognitionSnapshot Recognize(string logicalPath)
    {
        var safePath = SanitizeLogicalPath(logicalPath);

        try
        {
            var result = _engine.Recognize(new RecognitionRequest(safePath));
            var status = ResolveStatus(result);

            return new MediaRecognitionSnapshot(
                safePath,
                status,
                result.MediaKind.ToString(),
                result.SpecialKind.ToString(),
                result.EpisodePart.ToString(),
                result.IsFinalEpisode,
                result.Title,
                result.EpisodeTitle,
                result.TitleCandidates
                    .Select(candidate => new RecognitionTitleCandidateSnapshot(
                        candidate.Title,
                        candidate.Confidence,
                        candidate.Source,
                        candidate.IsPrimary))
                    .ToList(),
                result.SeasonNumber,
                result.CourNumber,
                result.EpisodeNumber,
                result.EpisodeEndNumber,
                result.SpecialNumber,
                result.Year,
                result.Confidence,
                result.ConfidenceLevel.ToString(),
                result.IsAmbiguous,
                result.Evidence
                    .Select(evidence => new RecognitionEvidenceSnapshot(
                        evidence.Code,
                        evidence.Value,
                        evidence.Weight))
                    .ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new MediaRecognitionSnapshot(
                safePath,
                MediaRecognitionStatus.Error,
                "Unknown",
                "None",
                "None",
                IsFinalEpisode: false,
                Title: null,
                EpisodeTitle: null,
                TitleCandidates: [],
                SeasonNumber: null,
                CourNumber: null,
                EpisodeNumber: null,
                EpisodeEndNumber: null,
                SpecialNumber: null,
                Year: null,
                Confidence: 0.0,
                ConfidenceLevel: "None",
                IsAmbiguous: false,
                Evidence: [],
                ErrorCode: ex.GetType().Name);
        }
    }

    public Task<MediaRecognitionSnapshot> RecognizeAsync(
        string logicalPath,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => Recognize(logicalPath),
            cancellationToken);

    public static string SanitizeLogicalPath(string? logicalPath)
    {
        var value = logicalPath?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return string.Empty;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps))
        {
            value = Uri.UnescapeDataString(uri.AbsolutePath);
        }
        else if (Path.IsPathRooted(value))
        {
            value = Path.GetFileName(value);
        }

        return value
            .Replace('\\', '/')
            .TrimStart('/');
    }

    private static (string Version, string AssemblyPath) GetRuntimeIdentity()
    {
        var assembly = typeof(RecognitionEngine).Assembly;
        var path = assembly.Location;
        Version? parsed = null;

        if (!string.IsNullOrWhiteSpace(path))
            Version.TryParse(FileVersionInfo.GetVersionInfo(path).FileVersion, out parsed);

        parsed ??= assembly.GetName().Version;
        var version = parsed is null
            ? "unknown"
            : $"{parsed.Major}.{Math.Max(0, parsed.Minor)}.{Math.Max(0, parsed.Build)}";

        return (version, path);
    }

    private static MediaRecognitionStatus ResolveStatus(
        RecognitionResult result)
    {
        if (result.IsAmbiguous)
            return MediaRecognitionStatus.Ambiguous;

        if (!string.IsNullOrWhiteSpace(result.Title) ||
            result.EpisodeNumber is not null ||
            result.SpecialNumber is not null ||
            result.MediaKind != MediaKind.Unknown)
        {
            return MediaRecognitionStatus.Recognized;
        }

        return MediaRecognitionStatus.Unresolved;
    }
}
