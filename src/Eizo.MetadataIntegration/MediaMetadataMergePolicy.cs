using Core = Eizo.Metadata.Core;

namespace Eizo.MetadataIntegration;

public sealed record MediaMetadataProviderSource(
    string Provider,
    Core.MetadataSubject Subject,
    Core.MetadataEpisode? Episode);

public sealed record MediaMetadataMergeResult(
    Core.MetadataSubject Subject,
    Core.MetadataEpisode? Episode,
    Dictionary<string, string> SeasonExternalIds,
    Dictionary<string, string> EpisodeExternalIds,
    Dictionary<string, string> FieldSources,
    List<string> Contributors);

public static class MediaMetadataMergePolicy
{
    public static MediaMetadataMergeResult Merge(
        MediaMetadataProviderSource primary,
        IEnumerable<MediaMetadataProviderSource>? supplements = null)
    {
        ArgumentNullException.ThrowIfNull(primary);

        var sources = new[] { primary }
            .Concat(supplements ?? [])
            .Where(static source =>
                source.Subject is not null &&
                !string.IsNullOrWhiteSpace(source.Provider))
            .GroupBy(
                static source => source.Provider,
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();

        var fieldSources = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        var subjectExternalIds =
            MergeSubjectExternalIds(sources);
        var titles = MergeTitles(
            sources,
            fieldSources);

        var overview = FirstValue(
            sources,
            static source => source.Subject.Overview,
            "Overview",
            fieldSources);
        var releaseDate = FirstValue(
            sources,
            static source => source.Subject.ReleaseDate,
            "ReleaseDate",
            fieldSources);
        var episodeCount = FirstValue(
            sources,
            static source => source.Subject.EpisodeCount,
            "EpisodeCount",
            fieldSources);

        var artwork = new Core.MetadataArtwork(
            FirstValue(
                sources,
                static source => source.Subject.Artwork.PosterUrl,
                "PosterUrl",
                fieldSources),
            FirstValue(
                sources,
                static source => source.Subject.Artwork.BackdropUrl,
                "BackdropUrl",
                fieldSources),
            FirstValue(
                sources,
                static source => source.Subject.Artwork.ThumbnailUrl,
                "ThumbnailUrl",
                fieldSources));

        var genres = MergeList(
            sources,
            static source => source.Subject.Genres,
            "Genres",
            fieldSources);
        var companies = MergeList(
            sources,
            static source => source.Subject.ProductionCompanies,
            "ProductionCompanies",
            fieldSources);
        var origins = MergeList(
            sources,
            static source => source.Subject.OriginCountryCodes,
            "OriginCountryCodes",
            fieldSources);

        var runtime = FirstValue(
            sources,
            static source => source.Subject.RuntimeMinutes,
            "RuntimeMinutes",
            fieldSources);
        var status = FirstValue(
            sources,
            static source => source.Subject.Status,
            "ProductionStatus",
            fieldSources);
        var language = FirstValue(
            sources,
            static source => source.Subject.OriginalLanguage,
            "OriginalLanguage",
            fieldSources);

        var cast = FirstNonEmptyList(
            sources,
            static source => source.Subject.Cast,
            "Cast",
            fieldSources);
        var crew = FirstNonEmptyList(
            sources,
            static source => source.Subject.Crew,
            "Crew",
            fieldSources);

        var contentKind = sources
            .Select(static source =>
                (source.Provider, source.Subject.ContentKind))
            .FirstOrDefault(static item =>
                item.ContentKind != Core.MetadataContentKind.Unknown);
        if (contentKind.ContentKind != Core.MetadataContentKind.Unknown)
        {
            fieldSources["ContentKind"] =
                contentKind.Provider;
        }

        var subject = primary.Subject with
        {
            Titles = titles,
            Overview = overview,
            ReleaseDate = releaseDate,
            EpisodeCount = episodeCount,
            Artwork = artwork,
            ExternalIds = subjectExternalIds,
            ContentKind =
                contentKind.ContentKind == Core.MetadataContentKind.Unknown
                    ? primary.Subject.ContentKind
                    : contentKind.ContentKind,
            Genres = genres,
            ProductionCompanies = companies,
            OriginCountryCodes = origins,
            RuntimeMinutes = runtime,
            Status = status,
            OriginalLanguage = language,
            Cast = cast,
            Crew = crew,
        };

        var episode = MergeEpisode(
            sources,
            fieldSources);
        var seasonExternalIds =
            MergeScopedExternalIds(
                sources,
                static source =>
                    source.Episode?.ProviderSeasonId);
        var episodeExternalIds =
            MergeScopedExternalIds(
                sources,
                static source =>
                    source.Episode?.ProviderEpisodeId);

        return new MediaMetadataMergeResult(
            subject,
            episode,
            seasonExternalIds,
            episodeExternalIds,
            fieldSources,
            sources
                .Select(static source => source.Provider)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static Core.MetadataTitles MergeTitles(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        IDictionary<string, string> fieldSources)
    {
        var primary = sources[0];
        var primaryTitle = primary.Subject.Titles.Primary;
        fieldSources["CanonicalTitle"] = primary.Provider;

        var original = FirstValue(
            sources,
            static source => source.Subject.Titles.Original,
            "OriginalTitle",
            fieldSources);

        var localized =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources.Reverse())
        {
            foreach (var pair in source.Subject.Titles.Localized)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    localized[pair.Key] = pair.Value;
            }
        }

        var aliases = sources
            .SelectMany(static source =>
                source.Subject.Titles.Aliases)
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new Core.MetadataTitles(
            primaryTitle,
            original,
            localized,
            aliases);
    }

    private static Dictionary<string, string>
        MergeSubjectExternalIds(
            IReadOnlyList<MediaMetadataProviderSource> sources)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources.Reverse())
        {
            foreach (var pair in source.Subject.ExternalIds)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    result[pair.Key] = pair.Value;
            }

            if (!string.IsNullOrWhiteSpace(
                    source.Subject.Id.Value))
            {
                result[source.Provider] =
                    source.Subject.Id.Value;
            }
        }

        return result;
    }

    private static Dictionary<string, string>
        MergeScopedExternalIds(
            IReadOnlyList<MediaMetadataProviderSource> sources,
            Func<MediaMetadataProviderSource, string?> selector)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources.Reverse())
        {
            var value = selector(source);
            if (!string.IsNullOrWhiteSpace(value))
                result[source.Provider] = value;
        }

        return result;
    }

    private static Core.MetadataEpisode? MergeEpisode(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        IDictionary<string, string> fieldSources)
    {
        var episodeSources = sources
            .Where(static source =>
                source.Episode is not null)
            .ToArray();
        if (episodeSources.Length == 0)
            return null;

        var basis = episodeSources[0];
        var basisEpisode = basis.Episode!;

        var primaryTitle = FirstValue(
            episodeSources,
            static source => source.Episode!.Titles.Primary,
            "EpisodeTitle",
            fieldSources) ??
            basisEpisode.Titles.Primary;
        var originalTitle = FirstValue(
            episodeSources,
            static source => source.Episode!.Titles.Original,
            "EpisodeOriginalTitle",
            fieldSources);
        var overview = FirstValue(
            episodeSources,
            static source => source.Episode!.Overview,
            "EpisodeOverview",
            fieldSources);
        var airDate = FirstValue(
            episodeSources,
            static source => source.Episode!.AirDate,
            "EpisodeAirDate",
            fieldSources);
        var thumbnail = FirstValue(
            episodeSources,
            static source => source.Episode!.ThumbnailUrl,
            "EpisodeThumbnailUrl",
            fieldSources);
        var seasonTitle = FirstValue(
            episodeSources,
            static source => source.Episode!.SeasonTitle,
            "SeasonTitle",
            fieldSources);
        var seasonOverview = FirstValue(
            episodeSources,
            static source => source.Episode!.SeasonOverview,
            "SeasonOverview",
            fieldSources);
        var seasonAirDate = FirstValue(
            episodeSources,
            static source => source.Episode!.SeasonAirDate,
            "SeasonAirDate",
            fieldSources);
        var seasonPoster = FirstValue(
            episodeSources,
            static source => source.Episode!.SeasonPosterUrl,
            "SeasonPosterUrl",
            fieldSources);

        var localized =
            new Dictionary<string, string>(
                basisEpisode.Titles.Localized,
                StringComparer.OrdinalIgnoreCase);
        var aliases = episodeSources
            .SelectMany(static source =>
                source.Episode!.Titles.Aliases)
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return basisEpisode with
        {
            Titles = new Core.MetadataTitles(
                primaryTitle,
                originalTitle,
                localized,
                aliases),
            Overview = overview,
            AirDate = airDate,
            ThumbnailUrl = thumbnail,
            SeasonTitle = seasonTitle,
            SeasonOverview = seasonOverview,
            SeasonAirDate = seasonAirDate,
            SeasonPosterUrl = seasonPoster,
        };
    }

    private static T? FirstValue<T>(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        Func<MediaMetadataProviderSource, T?> selector,
        string field,
        IDictionary<string, string> fieldSources)
    {
        foreach (var source in sources)
        {
            var value = selector(source);
            if (HasValue(value))
            {
                fieldSources[field] = source.Provider;
                return value;
            }
        }

        return default;
    }

    private static IReadOnlyList<T> FirstNonEmptyList<T>(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        Func<MediaMetadataProviderSource, IReadOnlyList<T>> selector,
        string field,
        IDictionary<string, string> fieldSources)
    {
        foreach (var source in sources)
        {
            var values = selector(source);
            if (values.Count == 0)
                continue;

            fieldSources[field] = source.Provider;
            return values.ToArray();
        }

        return Array.Empty<T>();
    }

    private static IReadOnlyList<string> MergeList(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        Func<MediaMetadataProviderSource, IReadOnlyList<string>> selector,
        string field,
        IDictionary<string, string> fieldSources)
    {
        var contributors = new List<string>();
        var values = new List<string>();

        foreach (var source in sources)
        {
            var sourceValues = selector(source)
                .Where(static value =>
                    !string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (sourceValues.Length == 0)
                continue;

            contributors.Add(source.Provider);
            values.AddRange(sourceValues);
        }

        if (contributors.Count > 0)
        {
            fieldSources[field] = string.Join(
                "+",
                contributors.Distinct(
                    StringComparer.OrdinalIgnoreCase));
        }

        return values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool HasValue<T>(T? value)
    {
        if (value is null)
            return false;

        if (value is string text)
            return !string.IsNullOrWhiteSpace(text);

        return true;
    }
}
