using System.Globalization;
using System.Text;
using Eizo.Media;
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
    IReadOnlyList<CatalogMediaItemModel> Items,
    EizoMedia Media,
    EizoSeries? Series)
{
    public string GroupingKey { get; init; } = Key;

    public MediaMetadataSnapshot? Metadata =>
        Items
            .Select(static item => item.Metadata)
            .OfType<MediaMetadataSnapshot>()
            .Where(static metadata => metadata.IsResolved)
            .OrderByDescending(static metadata =>
                !string.IsNullOrWhiteSpace(metadata.BackdropUrl))
            .ThenByDescending(static metadata =>
                !string.IsNullOrWhiteSpace(metadata.PosterUrl))
            .ThenByDescending(static metadata =>
                !string.IsNullOrWhiteSpace(metadata.CanonicalTitle) ||
                !string.IsNullOrWhiteSpace(metadata.OriginalTitle) ||
                !string.IsNullOrWhiteSpace(metadata.ReleaseDate))
            .FirstOrDefault();

    public int EpisodeCount => Episodes.Count;

    public bool IsMovieSubject =>
        Metadata?.SubjectKind == "Movie" ||
        GroupingBasis.Contains(
            "movie",
            StringComparison.OrdinalIgnoreCase) ||
        Items.Count > 0 &&
        Items.All(static item =>
            item.Media?.Format == MediaFormat.Movie ||
            string.Equals(
                item.Recognition?.MediaKind,
                "Movie",
                StringComparison.OrdinalIgnoreCase));

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
            .OrderByDescending(static item =>
                item.Metadata is { IsResolved: true } metadata &&
                !string.IsNullOrWhiteSpace(metadata.BackdropUrl))
            .ThenByDescending(static item =>
                item.Metadata is { IsResolved: true } metadata &&
                !string.IsNullOrWhiteSpace(metadata.PosterUrl))
            .ThenByDescending(static item => item.Metadata is { IsResolved: true })
            .ThenByDescending(static item => item.Recognition?.Confidence ?? 0)
            .First();

        var metadata = items
            .Select(static item => item.Metadata)
            .OfType<MediaMetadataSnapshot>()
            .Where(static value => value.IsResolved)
            .OrderByDescending(static value =>
                !string.IsNullOrWhiteSpace(value.BackdropUrl))
            .ThenByDescending(static value =>
                !string.IsNullOrWhiteSpace(value.PosterUrl))
            .ThenByDescending(static value =>
                !string.IsNullOrWhiteSpace(value.CanonicalTitle) ||
                !string.IsNullOrWhiteSpace(value.OriginalTitle) ||
                !string.IsNullOrWhiteSpace(value.ReleaseDate))
            .FirstOrDefault();

        var title = MediaTitleDisplayResolver.ResolvePrimary(
            metadata,
            identity.TitleHint,
            representative.Recognition?.Title,
            representative.DisplayTitle);

        var nativeTitle = MediaTitleDisplayResolver.ResolveSecondary(
            metadata,
            title,
            representative.SecondaryTitle);

        var category = CatalogCategoryClassifier.ResolveSubject(
            identity,
            items);

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

        var isMovieSubject =
            identity.Basis.Contains(
                "movie",
                StringComparison.OrdinalIgnoreCase) ||
            metadata?.SubjectKind == "Movie" ||
            items.All(static item =>
                string.Equals(
                    item.Recognition?.MediaKind,
                    "Movie",
                    StringComparison.OrdinalIgnoreCase));

        if (!isMovieSubject)
        {
            metaParts.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{episodes.Count} episodes"));
        }

        var subjectMedia = BuildSubjectMedia(
            identity,
            representative,
            title!,
            year,
            category,
            isMovieSubject,
            items);
        var series = isMovieSubject
            ? null
            : BuildSeries(subjectMedia, episodes);

        return new CatalogSubjectModel(
            subjectMedia.Id,
            identity.Basis,
            title!,
            nativeTitle ?? string.Empty,
            category,
            string.Join(" · ", metaParts),
            episodes,
            items.ToArray(),
            subjectMedia,
            series)
        {
            GroupingKey = identity.Key,
        };
    }

    private static EizoMedia BuildSubjectMedia(
        MediaSubjectGroupingIdentity identity,
        CatalogMediaItemModel representative,
        string title,
        int? year,
        MediaCategoryKind category,
        bool isMovieSubject,
        IReadOnlyList<CatalogMediaItemModel> items)
    {
        var mediaItems = items
            .Select(static item => item.Media)
            .OfType<EizoMedia>()
            .ToArray();

        var externalIds = MediaExternalIds.Common(
            mediaItems
                .Select(static media =>
                    (IEnumerable<KeyValuePair<string, string>>?)
                    media.ExternalIds)
                .ToArray());

        var format = ResolveSubjectFormat(
            isMovieSubject,
            mediaItems);
        var domain = ResolveSubjectDomain(
            category,
            mediaItems);
        var origin = ResolveSubjectOrigin(mediaItems);
        var identityTitle =
            identity.TitleHint ??
            representative.Recognition?.Title ??
            title;
        var identityYear =
            identity.Basis.Contains(
                "series-family",
                StringComparison.OrdinalIgnoreCase) ||
            identity.Basis.Contains(
                "movie-family",
                StringComparison.OrdinalIgnoreCase)
                ? null
                : representative.Recognition?.Year ?? year;
        var id = EizoMediaIdFactory.CreateInternal(
            identityTitle,
            identityYear,
            format);

        return new EizoMedia(
            id,
            format,
            domain,
            origin,
            externalIds);
    }

    private static EizoSeries BuildSeries(
        EizoMedia media,
        IReadOnlyList<CatalogEpisodeModel> episodes)
    {
        var seeds = episodes
            .Select(static episode =>
                new EizoHierarchyEpisodeSeed(
                    MediaCatalogStore.ItemKey(episode.PrimaryItem),
                    episode.SeasonNumber,
                    episode.EpisodeNumber,
                    episode.IsSpecial,
                    SeasonExternalIds:
                        MediaModelProjection.ProjectSeasonExternalIds(
                            episode.PrimaryItem.Metadata),
                    EpisodeExternalIds:
                        MediaModelProjection.ProjectEpisodeExternalIds(
                            episode.PrimaryItem.Metadata),
                    SeasonTitle:
                        episode.PrimaryItem.Metadata?.SeasonTitle,
                    SeasonOverview:
                        episode.PrimaryItem.Metadata?.SeasonOverview,
                    SeasonAirDate:
                        episode.PrimaryItem.Metadata?.SeasonAirDate,
                    SeasonPosterUrl:
                        episode.PrimaryItem.Metadata?.SeasonPosterUrl))
            .ToArray();

        return EizoMediaHierarchy.BuildSeries(media, seeds);
    }

    private static MediaFormat ResolveSubjectFormat(
        bool isMovieSubject,
        IReadOnlyList<EizoMedia> items)
    {
        if (isMovieSubject)
            return MediaFormat.Movie;

        var formats = items
            .Select(static item => item.Format)
            .Where(static format => format != MediaFormat.Unknown)
            .Distinct()
            .ToArray();

        return formats.Length == 1
            ? formats[0]
            : MediaFormat.TvSeries;
    }

    private static MediaContentDomain ResolveSubjectDomain(
        MediaCategoryKind category,
        IReadOnlyList<EizoMedia> items)
    {
        if (category == MediaCategoryKind.Anime)
            return MediaContentDomain.Animation;

        var domains = items
            .Select(static item => item.Domain)
            .Where(static domain => domain != MediaContentDomain.Unknown)
            .Distinct()
            .ToArray();

        return domains.Length == 1
            ? domains[0]
            : MediaContentDomain.Unknown;
    }

    private static MediaOrigin ResolveSubjectOrigin(
        IReadOnlyList<EizoMedia> items)
    {
        var countryCodes = items
            .SelectMany(static item => item.Origin.CountryCodes)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList();

        var primaryCountries = items
            .Select(static item => item.Origin.PrimaryCountryCode)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MediaOrigin(
            primaryCountries.Length == 1
                ? primaryCountries[0]
                : null,
            countryCodes);
    }

    private static IReadOnlyList<CatalogEpisodeModel> BuildEpisodes(
        IReadOnlyList<CatalogMediaItemModel> items)
    {
        var indexed = items
            .Select((item, index) =>
            {
                var grouping =
                    CatalogEpisodeGroupingResolver.Resolve(
                        item.SourceTitle,
                        item.Recognition?.MediaKind,
                        item.Recognition?.SpecialKind,
                        item.Recognition?.SeasonNumber,
                        item.Recognition?.EpisodeNumber,
                        item.Recognition?.SpecialNumber);

                return new IndexedEpisode(
                    item,
                    index,
                    grouping.SeasonNumber,
                    grouping.EpisodeNumber,
                    grouping.IsSpecial,
                    ResolveMovieIdentity(item));
            })
            .ToArray();

        return indexed
            .GroupBy(value =>
                value.Number is null
                    ? value.MovieIdentity is { Length: > 0 } movieIdentity
                        ? $"movie|{movieIdentity}"
                        : $"file|{value.Index}"
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
        var isSpecialGroup =
            values.Any(static value => value.Special);

        var selected = values
            .OrderByDescending(value =>
                isSpecialGroup &&
                CatalogEpisodeGroupingResolver.IsExplicitSpecialSource(
                    value.Item.SourceTitle))
            .ThenByDescending(static value =>
                value.Item.Location?.Kind == MediaLocationKind.LocalFile)
            .ThenByDescending(static value =>
                value.Item.Metadata is { IsResolved: true })
            .ThenByDescending(static value =>
                value.Item.Recognition?.Confidence ?? 0)
            .First();

        var primary = selected.Item;
        var metadata = primary.Metadata;
        var recognition = primary.Recognition;

        var isMovie =
            primary.Media?.Format == MediaFormat.Movie ||
            string.Equals(
                recognition?.MediaKind,
                "Movie",
                StringComparison.OrdinalIgnoreCase);

        string? title;
        string? nativeTitle;

        if (isMovie)
        {
            title = metadata?.CanonicalTitle;
            if (string.IsNullOrWhiteSpace(title))
                title = recognition?.Title;

            nativeTitle = metadata?.OriginalTitle;
        }
        else if (selected.Special)
        {
            // Metadata currently carries EpisodeNumber/EpisodeTitle but no
            // season/special identity. Never let a resolved S01E01 title
            // overwrite an explicit OVA/OAD/ONA/SP source.
            title = recognition?.EpisodeTitle;

            if (string.IsNullOrWhiteSpace(title))
            {
                title =
                    CatalogEpisodeGroupingResolver.ResolveExplicitSpecialLabel(
                        primary.SourceTitle,
                        selected.Number);
            }

            if (string.IsNullOrWhiteSpace(title) &&
                !CatalogEpisodeGroupingResolver.IsExplicitSpecialSource(
                    primary.SourceTitle))
            {
                title = metadata?.EpisodeTitle;
            }

            nativeTitle = string.Empty;
        }
        else
        {
            title = metadata?.EpisodeTitle;
            if (string.IsNullOrWhiteSpace(title))
                title = recognition?.EpisodeTitle;

            nativeTitle = metadata?.EpisodeOriginalTitle;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = selected.Number is not null
                ? selected.Special
                    ? $"Special {FormatEpisodeNumber((decimal)selected.Number)}"
                    : $"Episode {FormatEpisodeNumber((decimal)selected.Number)}"
                : primary.SourceTitle;
        }

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
        bool Special,
        string? MovieIdentity);

    private static string? ResolveMovieIdentity(
        CatalogMediaItemModel item)
    {
        if (item.Media?.Format != MediaFormat.Movie &&
            !string.Equals(
                item.Recognition?.MediaKind,
                "Movie",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (item.Media is { Format: MediaFormat.Movie, Id.Length: > 0 } media)
        {
            return media.Id;
        }

        if (item.Metadata is
            {
                IsResolved: true,
                Provider.Length: > 0,
                ProviderSubjectId.Length: > 0
            } metadata)
        {
            return $"{metadata.Provider!.Trim().ToLowerInvariant()}|{metadata.ProviderSubjectId}";
        }

        var title = item.Recognition?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalized = title
            .Normalize(NormalizationForm.FormKC)
            .ToUpperInvariant();
        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0
            ? null
            : $"{builder}|{item.Recognition?.Year?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
    }

    private static string FormatEpisodeNumber(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
}

internal static class CatalogCategoryClassifier
{
    public static MediaCategoryKind? Resolve(CatalogMediaItemModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Category is { } explicitCategory)
        {
            return explicitCategory;
        }

        if (item.Media is { } media)
        {
            if (media.Domain == MediaContentDomain.Animation)
                return MediaCategoryKind.Anime;

            if (media.Domain is
                MediaContentDomain.LiveAction or
                MediaContentDomain.Documentary)
            {
                return media.Format == MediaFormat.Movie
                    ? MediaCategoryKind.Movies
                    : MediaCategoryKind.Series;
            }

            if (media.Format == MediaFormat.Movie)
                return MediaCategoryKind.Movies;
        }

        return Map(MediaLibraryGrouping.Classify(
            item.Recognition,
            item.Metadata));
    }

    public static MediaCategoryKind ResolveSubject(
        MediaSubjectGroupingIdentity identity,
        IReadOnlyList<CatalogMediaItemModel> items)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(items);

        var categories = items
            .Select(Resolve)
            .Where(static value => value is not null)
            .Select(static value => value!.Value)
            .ToArray();

        if (categories.Contains(MediaCategoryKind.Anime))
        {
            return MediaCategoryKind.Anime;
        }

        if (categories.Contains(MediaCategoryKind.Movies))
        {
            return MediaCategoryKind.Movies;
        }

        if (categories.Contains(MediaCategoryKind.Series))
        {
            return MediaCategoryKind.Series;
        }

        return identity.Basis.Contains(
                "movie",
                StringComparison.OrdinalIgnoreCase)
            ? MediaCategoryKind.Movies
            : MediaCategoryKind.Series;
    }

    private static MediaCategoryKind? Map(
        MediaLibraryCategoryHint hint) =>
        hint switch
        {
            MediaLibraryCategoryHint.Anime => MediaCategoryKind.Anime,
            MediaLibraryCategoryHint.Series => MediaCategoryKind.Series,
            MediaLibraryCategoryHint.Movies => MediaCategoryKind.Movies,
            _ => null,
        };
}
