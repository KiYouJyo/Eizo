using System.Text.Json.Serialization;

namespace Eizo.Recognition;

public enum MediaRecognitionStatus
{
    Unresolved = 0,
    Recognized = 1,
    Ambiguous = 2,
    Error = 3
}

public sealed record RecognitionEvidenceSnapshot(
    string Code,
    string? Value,
    double Weight);

public sealed record RecognitionTitleCandidateSnapshot(
    string Title,
    double Confidence,
    string Source,
    bool IsPrimary);

public sealed record MediaRecognitionSnapshot(
    string LogicalPath,
    MediaRecognitionStatus Status,
    string MediaKind,
    string SpecialKind,
    string EpisodePart,
    bool IsFinalEpisode,
    string? Title,
    string? EpisodeTitle,
    List<RecognitionTitleCandidateSnapshot> TitleCandidates,
    int? SeasonNumber,
    int? CourNumber,
    decimal? EpisodeNumber,
    decimal? EpisodeEndNumber,
    decimal? SpecialNumber,
    int? Year,
    double Confidence,
    string ConfidenceLevel,
    bool IsAmbiguous,
    List<RecognitionEvidenceSnapshot> Evidence,
    string? ErrorCode = null)
{
    [JsonIgnore]
    public bool ShouldApplyDisplayTitle =>
        Status == MediaRecognitionStatus.Recognized &&
        !string.IsNullOrWhiteSpace(Title) &&
        ConfidenceLevel is "Medium" or "High";
}
