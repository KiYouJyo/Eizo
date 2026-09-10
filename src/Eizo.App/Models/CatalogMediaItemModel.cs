namespace Eizo.Models;

public enum MediaLocationKind
{
    LocalFile = 0,
    RemoteUri = 1
}

public enum CatalogRecognitionMediaKind
{
    Unknown = 0,
    SeriesEpisode = 1,
    Movie = 2,
    Special = 3
}

public enum CatalogRecognitionSpecialKind
{
    None = 0,
    Ova = 1,
    Oad = 2,
    Ona = 3,
    Special = 4,
    NcOp = 5,
    NcEd = 6
}

public enum CatalogRecognitionEpisodePart
{
    None = 0,
    First = 1,
    Second = 2
}

public enum CatalogRecognitionConfidenceLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public sealed record MediaLocationModel(
    string SourceId,
    MediaLocationKind Kind,
    string Locator,
    long? SizeBytes = null,
    DateTimeOffset? ModifiedUtc = null);

public sealed record CatalogRecognitionTitleCandidateModel(
    string Title,
    double Confidence,
    string Source,
    bool IsPrimary);

public sealed record CatalogRecognitionModel(
    CatalogRecognitionMediaKind MediaKind,
    CatalogRecognitionSpecialKind SpecialKind,
    CatalogRecognitionEpisodePart EpisodePart,
    bool IsFinalEpisode,
    string? Title,
    List<CatalogRecognitionTitleCandidateModel> TitleCandidates,
    int? SeasonNumber,
    int? CourNumber,
    decimal? EpisodeNumber,
    decimal? EpisodeEndNumber,
    decimal? SpecialNumber,
    int? Year,
    double Confidence,
    CatalogRecognitionConfidenceLevel ConfidenceLevel,
    bool IsAmbiguous);

public sealed record CatalogMediaItemModel(
    string SourceTitle,
    string? ParsedTitle,
    string? NativeTitle,
    MediaCategoryKind? Category,
    string Meta,
    MediaLocationModel? Location = null,
    CatalogRecognitionModel? Recognition = null)
{
    public bool IsParsed => !string.IsNullOrWhiteSpace(ParsedTitle);

    public string DisplayTitle =>
        IsParsed
            ? ParsedTitle!
            : SourceTitle;

    public string SecondaryTitle =>
        IsParsed && !string.IsNullOrWhiteSpace(NativeTitle)
            ? NativeTitle!
            : SourceTitle;

    public string? LocalPath =>
        Location is { Kind: MediaLocationKind.LocalFile }
            ? Location.Locator
            : null;
}
