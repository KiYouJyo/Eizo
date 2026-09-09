namespace Eizo.Models;

public sealed record CatalogMediaItemModel(
    string SourceTitle,
    string? ParsedTitle,
    string? NativeTitle,
    MediaCategoryKind? Category,
    string Meta,
    string? SourcePath = null)
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
}
