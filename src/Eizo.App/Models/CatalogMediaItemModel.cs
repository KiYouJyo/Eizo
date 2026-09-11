using Eizo.MetadataIntegration;
using Eizo.Recognition;

namespace Eizo.Models;

public enum MediaLocationKind
{
    LocalFile = 0,
    RemoteUri = 1
}

public sealed record MediaLocationModel(
    string SourceId,
    MediaLocationKind Kind,
    string Locator,
    long? SizeBytes = null,
    DateTimeOffset? ModifiedUtc = null);

public sealed record CatalogMediaItemModel(
    string SourceTitle,
    string? ParsedTitle,
    string? NativeTitle,
    MediaCategoryKind? Category,
    string Meta,
    MediaLocationModel? Location = null,
    MediaRecognitionSnapshot? Recognition = null,
    MediaMetadataSnapshot? Metadata = null)
{
    public bool IsParsed => !string.IsNullOrWhiteSpace(ParsedTitle);

    public string DisplayTitle =>
        Metadata is { IsResolved: true, CanonicalTitle.Length: > 0 } metadata
            ? metadata.CanonicalTitle!
            : IsParsed
                ? ParsedTitle!
                : SourceTitle;

    public string SecondaryTitle
    {
        get
        {
            if (Metadata is { IsResolved: true } metadata &&
                !string.IsNullOrWhiteSpace(metadata.OriginalTitle) &&
                !string.Equals(
                    metadata.OriginalTitle,
                    DisplayTitle,
                    StringComparison.CurrentCultureIgnoreCase))
            {
                return metadata.OriginalTitle!;
            }

            return IsParsed && !string.IsNullOrWhiteSpace(NativeTitle)
                ? NativeTitle!
                : SourceTitle;
        }
    }

    public string? LocalPath =>
        Location is { Kind: MediaLocationKind.LocalFile }
            ? Location.Locator
            : null;
}
