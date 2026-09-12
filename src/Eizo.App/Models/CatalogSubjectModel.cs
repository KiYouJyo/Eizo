using System.Globalization;
using Eizo.MetadataIntegration;

namespace Eizo.Models;

public sealed record CatalogEpisodeModel(
    int? SeasonNumber,
    decimal? EpisodeNumber,
    bool IsSpecial,
    string Title,
    string NativeTitle,
    CatalogMediaItemModel PrimaryItem,
    IReadOnlyList<CatalogMediaItemModel> AlternateItems)
{
    public int SourceCount => 1 + AlternateItems.Count;
}

public sealed record CatalogSubjectModel(
    string Key,
    string GroupingBasis,
    string Title,
    string NativeTitle,
    MediaCategoryKind Category,
    string Meta,
    IReadOnlyList<CatalogEpisodeModel> Episodes,
    IReadOnlyList<CatalogMediaItemModel> Items)
{
    public MediaMetadataSnapshot? Metadata =>
        Items
            .Select(static item => item.Metadata)
            .FirstOrDefault(static metadata => metadata is { IsResolved: true });

    public int EpisodeCount => Episodes.Count;

    public IReadOnlyList<int> SeasonNumbers =>
        Episodes
            .Select(static episode => episode.SeasonNumber ?? 1)
            .Distinct()
            .OrderBy(static season => season == 0 ? int.MaxValue : season)
            .ToArray();

    public CatalogMediaItemModel? FirstPlayableItem =>
        Episodes.FirstOrDefault()?.PrimaryItem;
}

public sealed record CatalogLibraryAggregation(
    IReadOnlyList<CatalogSubjectModel> Subjects,
    IReadOnlyList<CatalogMediaItemModel> StandaloneItems);

internal static class CatalogSubjectAggregator
{
    public static CatalogLibraryAggregation Build(
        IReadOnlyList<CatalogMediaItemModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var grouped = new Dictionary<
            string,
            (MediaSubjectGroupingIdentity Identity, List<CatalogMediaItemModel> Items)>(
            StringComparer.Ordinal);
        var standalone = new List<CatalogMediaItemModel>();

        var groupingInputs = items
            .Select((item, index) =>
                new MediaLibraryGroupingInput(
                    index.ToString(CultureInfo.InvariantCulture),
                    item.Recognition,
                    item.Metadata))
            .ToArray();
        var assignments = MediaLibraryGrouping
            .AssignSubjectIdentities(groupingInputs)
            .ToDictionary(
                static assignment => assignment.ItemKey,
                static assignment => assignment.Identity,
                StringComparer.Ordinal);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var itemKey = index.ToString(CultureInfo.InvariantCulture);

            if (!assignments.TryGetValue(itemKey, out var identity) ||
                identity is null)
            {
                standalone.Add(item);
                continue;
            }

            if (!grouped.TryGetValue(identity.Key, out var bucket))
            {
                bucket = (identity, []);
                grouped.Add(identity.Key, bucket);
            }

            bucket.Items.Add(item);
        }

        var subjects = grouped.Values
            .Select(static bucket => CreateSubject(
                bucket.Identity,
                bucket.Items))
            .OrderBy(static subject => subject.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new CatalogLibraryAggregation(
            subjects,
            standalone);
    }

    private static CatalogSubjectModel CreateSubject(
        MediaSubjectGroupingIdentity identity,
        IReadOnlyList<CatalogMediaItemModel> items)
    {
        var representative = items
            .OrderByDescending(static item => item.Metadata is { IsResolved: true })
            .ThenByDescending(static item => item.Recognition?.Confidence ?? 0)
            .First();

        var metadata = representative.Metadata is { IsResolved: true }
            ? representative.Metadata
            : items
                .Select(static item => item.Metadata)
                .FirstOrDefault(static value => value is { IsResolved: true });

        var title = metadata?.CanonicalTitle;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = representative.Recognition?.Title;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = representative.DisplayTitle;
        }

        var nativeTitle = metadata?.OriginalTitle;
        if (string.IsNullOrWhiteSpace(nativeTitle) ||
            string.Equals(
                nativeTitle,
                title,
                StringComparison.CurrentCultureIgnoreCase))
        {
            nativeTitle = representative.SecondaryTitle;
        }

        var category = items
            .Select(static item => item.Category)
            .FirstOrDefault(static value => value is not null)
            ?? MediaCategoryKind.Series;

        var episodes = BuildEpisodes(items);
        var year = metadata?.ReleaseDate is { Length: > 0 } releaseDate &&
                   DateOnly.TryParse(releaseDate, out var parsedDate)
            ? parsedDate.Year
            : representative.Recognition?.Year;

        var metaParts = new List<string>();
        if (year is not null)
        {
            metaParts.Add(year.Value.ToString(CultureInfo.InvariantCulture));
        }

        metaParts.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{episodes.Count} episodes"));

        if (metadata is { IsResolved: true, Provider.Length: > 0 })
        {
            metaParts.Add(metadata.Provider!);
        }

        return new CatalogSubjectModel(
            identity.Key,
            identity.Basis,
            title!,
            nativeTitle ?? string.Empty,
            category,
            string.Join(" · ", metaParts),
            episodes,
            items.ToArray());
    }

    private static IReadOnlyList<CatalogEpisodeModel> BuildEpisodes(
        IReadOnlyList<CatalogMediaItemModel> items)
    {
        var indexed = items
            .Select((item, index) => new IndexedEpisode(
                item,
                index,
                ResolveSeason(item),
                item.Recognition?.EpisodeNumber
                ?? item.Recognition?.SpecialNumber,
                string.Equals(
                    item.Recognition?.MediaKind,
                    "Special",
                    StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return indexed
            .GroupBy(value =>
                value.Number is null
                    ? $"file|{value.Index}"
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{value.Season}|{value.Number}|{value.Special}"),
                StringComparer.Ordinal)
            .Select(static group => CreateEpisode(group.ToArray()))
            .OrderBy(static episode =>
                episode.SeasonNumber == 0
                    ? int.MaxValue
                    : episode.SeasonNumber ?? 1)
            .ThenBy(static episode => episode.EpisodeNumber ?? decimal.MaxValue)
            .ThenBy(static episode => episode.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static CatalogEpisodeModel CreateEpisode(
        IReadOnlyList<IndexedEpisode> values)
    {
        var selected = values
            .OrderByDescending(static value =>
                value.Item.Location?.Kind == MediaLocationKind.LocalFile)
            .ThenByDescending(static value =>
                value.Item.Metadata is { IsResolved: true })
            .ThenByDescending(static value =>
                value.Item.Recognition?.Confidence ?? 0)
            .First();

        var primary = selected.Item;
        var metadata = primary.Metadata;
        var recognition = primary.Recognition;

        var title = metadata?.EpisodeTitle;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = recognition?.EpisodeTitle;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = selected.Number is not null
                ? $"Episode {FormatEpisodeNumber((decimal)selected.Number)}"
                : primary.SourceTitle;
        }

        var nativeTitle = metadata?.EpisodeOriginalTitle;
        if (string.IsNullOrWhiteSpace(nativeTitle) ||
            string.Equals(
                nativeTitle,
                title,
                StringComparison.CurrentCultureIgnoreCase))
        {
            nativeTitle = string.Empty;
        }

        return new CatalogEpisodeModel(
            (int?)selected.Season,
            (decimal?)selected.Number,
            (bool)selected.Special,
            title!,
            nativeTitle ?? string.Empty,
            primary,
            values
                .Where(value => value.Index != selected.Index)
                .Select(static value => value.Item)
                .ToArray());
    }

    private sealed record IndexedEpisode(
        CatalogMediaItemModel Item,
        int Index,
        int Season,
        decimal? Number,
        bool Special);

    private static int ResolveSeason(CatalogMediaItemModel item)
    {
        if (string.Equals(
                item.Recognition?.MediaKind,
                "Special",
                StringComparison.OrdinalIgnoreCase))
        {
            return item.Recognition?.SeasonNumber ?? 0;
        }

        return item.Recognition?.SeasonNumber ?? 1;
    }

    private static string FormatEpisodeNumber(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
}
