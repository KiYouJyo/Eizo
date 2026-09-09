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
    MediaLocationModel? Location = null)
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
