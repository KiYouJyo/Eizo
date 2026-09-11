namespace Eizo.MetadataIntegration;

public enum MediaMetadataStatus
{
    Unresolved = 0,
    Resolved = 1,
    Error = 2,
}

public sealed record MetadataProviderErrorSnapshot(
    string Provider,
    string ErrorType,
    string Message);

public sealed record MetadataResolutionCandidateSnapshot(
    string Provider,
    string ProviderSubjectId,
    string SubjectKind,
    string Title,
    int? Year,
    int ProviderRank,
    double Score,
    List<string> Evidence);

public sealed record MediaMetadataSnapshot(
    string RuntimeVersion,
    string? RecognitionRuntimeVersion,
    MediaMetadataStatus Status,
    string? Provider,
    string? ProviderSubjectId,
    string? SubjectKind,
    string? CanonicalTitle,
    string? OriginalTitle,
    Dictionary<string, string> LocalizedTitles,
    List<string> Aliases,
    string? Overview,
    string? ReleaseDate,
    int? EpisodeCount,
    string? PosterUrl,
    string? BackdropUrl,
    Dictionary<string, string> ExternalIds,
    decimal? EpisodeNumber,
    string? EpisodeTitle,
    string? EpisodeOriginalTitle,
    string? EpisodeOverview,
    string? EpisodeAirDate,
    string? EpisodeThumbnailUrl,
    double Confidence,
    List<MetadataProviderErrorSnapshot> Errors,
    DateTimeOffset UpdatedAtUtc)
{
    public string? ResolutionReason { get; init; }

    public List<string> SearchTitles { get; init; } = [];

    public int CandidateCount { get; init; }

    public double AutoResolveThreshold { get; init; } = 0.82;

    public double MinimumLead { get; init; } = 0.06;

    public double? BestScore { get; init; }

    public double? SecondScore { get; init; }

    public double? Lead { get; init; }

    public List<MetadataResolutionCandidateSnapshot> TopCandidates { get; init; } = [];

    public bool IsResolved =>
        Status == MediaMetadataStatus.Resolved &&
        !string.IsNullOrWhiteSpace(Provider) &&
        !string.IsNullOrWhiteSpace(ProviderSubjectId);

    public bool MatchesRecognitionRuntime(string? runtimeVersion) =>
        string.Equals(
            RecognitionRuntimeVersion,
            runtimeVersion,
            StringComparison.OrdinalIgnoreCase);
}

public sealed record MetadataRuntimeIdentity(
    string Version,
    string CoreAssemblyPath,
    string ProvidersAssemblyPath,
    string ProbeStatus);
