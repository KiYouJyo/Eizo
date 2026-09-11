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
