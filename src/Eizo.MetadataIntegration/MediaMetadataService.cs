using System.Diagnostics;
using Core = Eizo.Metadata.Core;
using Provider = Eizo.Metadata.Providers;
using RecognitionContracts = Eizo.Metadata.Recognition;
using HostRecognition = Eizo.Recognition;

namespace Eizo.MetadataIntegration;

public sealed record MediaMetadataServiceOptions(
    bool EnableBangumi = true,
    string BangumiUserAgent = "KiYouJyo/Eizo/0.5.11 (https://github.com/KiYouJyo/Eizo)",
    string PreferredLanguage = "zh-CN",
    string? TmdbReadAccessToken = null,
    string? CacheDirectory = null,
    bool EnableArtworkProviders = true,
    string AniListUserAgent = "KiYouJyo/Eizo/0.5.11 (https://github.com/KiYouJyo/Eizo)");

public sealed class MediaMetadataService
{
    private const double AutoResolveThreshold = 0.82;
    private const double MinimumLead = 0.06;
    private const int DiagnosticCandidateLimit = 5;

    private static readonly HttpClient SharedBangumiHttpClient = CreateHttpClient();
    private static readonly HttpClient SharedTmdbHttpClient = CreateHttpClient();

    private readonly Core.MetadataResolver? _resolver;
    private readonly IReadOnlyDictionary<string, Core.MetadataResolver>
        _providerResolvers;
    private readonly IReadOnlyDictionary<string, Core.IMetadataProvider>
        _providers;
    private readonly Core.MetadataArtworkResolver? _artworkResolver;
    private readonly Core.IMetadataProvider? _tmdbProvider;
    private readonly string _preferredLanguage;

    public MediaMetadataService(
        MediaMetadataServiceOptions? options = null,
        HttpClient? bangumiHttpClient = null,
        HttpClient? tmdbHttpClient = null,
        HttpClient? anilistHttpClient = null)
    {
        options ??= new MediaMetadataServiceOptions();
        _preferredLanguage = string.IsNullOrWhiteSpace(options.PreferredLanguage)
            ? "zh-CN"
            : options.PreferredLanguage;

        var cacheBase = string.IsNullOrWhiteSpace(options.CacheDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Eizo",
                "MetadataCache")
            : Path.GetFullPath(options.CacheDirectory);

        // Provider cache payloads serialize Metadata runtime contracts. Never
        // reuse them across runtime versions: older Candidate/Subject JSON can
        // deserialize successfully while silently defaulting newly added
        // fields (for example ContentKind) to Unknown.
        var cacheRoot = Path.Combine(
            cacheBase,
            $"runtime-{RuntimeVersion}");

        var fileCache = new Core.FileMetadataCache(cacheRoot);
        var memoryCache = new Core.MemoryMetadataCache();
        var providers = new List<Core.IMetadataProvider>();
        var providerResolvers =
            new Dictionary<string, Core.MetadataResolver>(
                StringComparer.OrdinalIgnoreCase);
        var providerMap =
            new Dictionary<string, Core.IMetadataProvider>(
                StringComparer.OrdinalIgnoreCase);
        var resolverOptions =
            new Core.MetadataResolverOptions(
                AutoResolveThreshold,
                MinimumLead);
        var artworkProviders =
            new List<Core.IMetadataArtworkProvider>();

        // AniList is intentionally skipped in the current roadmap.
        // Artwork enhancement is provided by TMDB plus local fallbacks.
        _ = anilistHttpClient;

        if (options.EnableBangumi)
        {
            var bangumi = new Provider.BangumiMetadataProvider(
                bangumiHttpClient ?? SharedBangumiHttpClient,
                new Provider.BangumiMetadataProviderOptions(
                    options.BangumiUserAgent));

            var persistentBangumi = new Core.CachedMetadataProvider(
                bangumi,
                fileCache,
                Core.MetadataCachePolicy.Default);

            var cachedBangumi = new Core.CachedMetadataProvider(
                persistentBangumi,
                memoryCache,
                Core.MetadataCachePolicy.Default);
            providers.Add(cachedBangumi);
            providerMap["bangumi"] = cachedBangumi;
            providerResolvers["bangumi"] =
                new Core.MetadataResolver(
                    [cachedBangumi],
                    resolverOptions);
        }

        if (!string.IsNullOrWhiteSpace(options.TmdbReadAccessToken))
        {
            var tmdb = new Provider.TmdbMetadataProvider(
                tmdbHttpClient ?? SharedTmdbHttpClient,
                new Provider.TmdbMetadataProviderOptions(
                    options.TmdbReadAccessToken,
                    _preferredLanguage));

            var persistentTmdb = new Core.CachedMetadataProvider(
                tmdb,
                fileCache,
                Core.MetadataCachePolicy.Default);

            _tmdbProvider = new Core.CachedMetadataProvider(
                persistentTmdb,
                memoryCache,
                Core.MetadataCachePolicy.Default);
            providers.Add(_tmdbProvider);
            providerMap["tmdb"] = _tmdbProvider;
            providerResolvers["tmdb"] =
                new Core.MetadataResolver(
                    [_tmdbProvider],
                    resolverOptions);

            if (options.EnableArtworkProviders)
            {
                Core.IMetadataArtworkProvider tmdbArtwork =
                    new Core.CachedMetadataArtworkProvider(
                        tmdb,
                        fileCache);

                artworkProviders.Add(
                    new Core.CachedMetadataArtworkProvider(
                        tmdbArtwork,
                        memoryCache));
            }
        }

        _providerResolvers = providerResolvers;
        _providers = providerMap;
        _resolver = providers.Count == 0
            ? null
            : new Core.MetadataResolver(
                providers,
                resolverOptions);

        _artworkResolver = artworkProviders.Count == 0
            ? null
            : new Core.MetadataArtworkResolver(
                artworkProviders);
    }

    public static string RuntimeVersion => ProbeRuntime().Version;

    public bool IsAvailable => _resolver is not null;

    public async Task<IReadOnlyList<MediaMetadataMatchCandidate>>
        SearchCandidatesAsync(
            HostRecognition.MediaRecognitionSnapshot recognition,
            string? query = null,
            string? provider = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recognition);

        var mediaKind = ParseMediaKind(
            recognition.MediaKind);
        if (_resolver is null ||
            mediaKind == RecognitionContracts.MediaKind.Unknown)
        {
            return Array.Empty<MediaMetadataMatchCandidate>();
        }

        var titles = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            AddTitle(titles, query);
        }
        else
        {
            AddTitle(titles, recognition.Title);
            foreach (var candidate in recognition.TitleCandidates
                         .OrderByDescending(static item => item.IsPrimary)
                         .ThenByDescending(static item => item.Confidence))
            {
                AddTitle(titles, candidate.Title);
                if (titles.Count >= 4)
                    break;
            }
        }

        if (titles.Count == 0)
            return Array.Empty<MediaMetadataMatchCandidate>();

        var request = new Core.MetadataSearchRequest(
            titles,
            recognition.Year,
            mediaKind,
            recognition.SeasonNumber,
            recognition.EpisodeNumber ??
            recognition.SpecialNumber,
            _preferredLanguage,
            Limit: 20);

        Core.MetadataResolution resolution;
        if (!string.IsNullOrWhiteSpace(provider) &&
            _providerResolvers.TryGetValue(
                provider,
                out var providerResolver))
        {
            resolution = await providerResolver
                .ResolveAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            resolution = await _resolver
                .ResolveAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return resolution.Candidates
            .GroupBy(
                static item =>
                    $"{item.Candidate.Id.Provider}|{item.Candidate.Id.Value}",
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(static item => item.Score)
                .First())
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item =>
                item.Candidate.ProviderRank)
            .Take(20)
            .Select(static item =>
                new MediaMetadataMatchCandidate(
                    item.Candidate.Id.Provider,
                    item.Candidate.Id.Value,
                    item.Candidate.Id.Kind.ToString(),
                    item.Candidate.Titles.Primary,
                    item.Candidate.Titles.Original,
                    item.Candidate.Year,
                    item.Candidate.ContentKind.ToString(),
                    item.Score))
            .ToArray();
    }

    public Task<MediaMetadataSnapshot?> EnrichAsync(
        HostRecognition.MediaRecognitionSnapshot recognition,
        CancellationToken cancellationToken = default) =>
        EnrichAsync(
            recognition,
            binding: null,
            cancellationToken);

    public async Task<MediaMetadataSnapshot?> EnrichAsync(
        HostRecognition.MediaRecognitionSnapshot recognition,
        MediaIdentityBindingHint? binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recognition);

        if (_resolver is null ||
            recognition.Status != HostRecognition.MediaRecognitionStatus.Recognized ||
            string.IsNullOrWhiteSpace(recognition.Title) ||
            recognition.ConfidenceLevel is not ("Medium" or "High"))
        {
            return null;
        }

        var mediaKind = ParseMediaKind(recognition.MediaKind);
        if (mediaKind == RecognitionContracts.MediaKind.Unknown)
        {
            return null;
        }

        var titles = new List<string>();
        AddTitle(titles, recognition.Title);

        foreach (var candidate in recognition.TitleCandidates
                     .OrderByDescending(static item => item.IsPrimary)
                     .ThenByDescending(static item => item.Confidence))
        {
            AddTitle(titles, candidate.Title);
            if (titles.Count >= 4)
            {
                break;
            }
        }

        var request = new Core.MetadataSearchRequest(
            titles,
            recognition.Year,
            mediaKind,
            recognition.SeasonNumber,
            recognition.EpisodeNumber ?? recognition.SpecialNumber,
            _preferredLanguage,
            Limit: 10);
        var providerRequest = request.ForProviderSearch();

        Core.MetadataEnrichmentResult result;
        MediaProviderRoute route;
        try
        {
            var aggregateResolution = await _resolver
                .ResolveAsync(request, cancellationToken)
                .ConfigureAwait(false);
            route = MediaProviderRouter.Choose(
                aggregateResolution);

            if (binding is not null &&
                !string.IsNullOrWhiteSpace(
                    binding.PrimaryProvider) &&
                binding.ExternalIds.ContainsKey(
                    binding.PrimaryProvider))
            {
                route = new MediaProviderRoute(
                    binding.PrimaryProvider,
                    route.EnumerateProviders()
                        .Where(provider =>
                            !string.Equals(
                                provider,
                                binding.PrimaryProvider,
                                StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    binding.IsManual
                        ? "ManualIdentityBinding"
                        : "PersistedIdentityBinding");
            }

            result = await EnrichWithRouteAsync(
                    request,
                    route,
                    binding,
                    cancellationToken)
                .ConfigureAwait(false);

            var combinedErrors = aggregateResolution.ProviderErrors
                .Concat(result.ProviderErrors)
                .Distinct()
                .ToArray();

            result = result with
            {
                Resolution = result.Resolution with
                {
                    Candidates =
                        aggregateResolution.Candidates,
                    ProviderErrors = combinedErrors,
                },
                ProviderErrors = combinedErrors,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BuildFailure(
                recognition,
                "metadata",
                exception.GetType().Name,
                exception.Message);
        }

        var errors = result.ProviderErrors
            .Select(static error => new MetadataProviderErrorSnapshot(
                error.Provider,
                error.ErrorType,
                error.Message))
            .ToList();

        if (!result.Resolution.IsResolved ||
            result.Resolution.Best is null ||
            result.Subject is null)
        {
            return WithRouteDiagnostics(
                WithResolutionDiagnostics(
                new MediaMetadataSnapshot(
                    RuntimeVersion,
                    recognition.RuntimeVersion,
                    MediaMetadataStatus.Unresolved,
                    Provider: null,
                    ProviderSubjectId: null,
                    SubjectKind: null,
                    CanonicalTitle: null,
                    OriginalTitle: null,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    [],
                    Overview: null,
                    ReleaseDate: null,
                    EpisodeCount: null,
                    PosterUrl: null,
                    BackdropUrl: null,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    EpisodeNumber: recognition.EpisodeNumber ?? recognition.SpecialNumber,
                    EpisodeTitle: null,
                    EpisodeOriginalTitle: null,
                    EpisodeOverview: null,
                    EpisodeAirDate: null,
                    EpisodeThumbnailUrl: null,
                    result.Resolution.Confidence,
                    errors,
                    DateTimeOffset.UtcNow),
                providerRequest,
                result.Resolution,
                result.Subject),
                route);
        }

        var supplementalResults =
            await LoadSupplementalResultsAsync(
                    request,
                    route,
                    result.Subject.Id.Provider,
                    binding,
                    cancellationToken)
                .ConfigureAwait(false);

        errors.AddRange(
            supplementalResults
                .SelectMany(static item =>
                    item.ProviderErrors)
                .Select(static error =>
                    new MetadataProviderErrorSnapshot(
                        error.Provider,
                        error.ErrorType,
                        error.Message)));

        var mergeResult = MediaMetadataMergePolicy.Merge(
            new MediaMetadataProviderSource(
                result.Subject.Id.Provider,
                result.Subject,
                result.Episode),
            supplementalResults
                .Where(static item =>
                    item.Subject is not null)
                .Select(static item =>
                    new MediaMetadataProviderSource(
                        item.Subject!.Id.Provider,
                        item.Subject!,
                        item.Episode)));

        var subject = mergeResult.Subject;
        var episode = mergeResult.Episode;
        var artwork = subject.Artwork;
        IReadOnlyList<string> artworkContributors =
            Array.Empty<string>();

        if (_artworkResolver is not null &&
            string.IsNullOrWhiteSpace(artwork.BackdropUrl))
        {
            try
            {
                var artworkResult = await _artworkResolver
                    .ResolveAsync(
                        new Core.MetadataArtworkRequest(
                            subject.Titles
                                .EnumerateAll()
                                .Where(static title =>
                                    !string.IsNullOrWhiteSpace(title))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Take(8)
                                .ToArray(),
                            subject.ReleaseDate?.Year ??
                            recognition.Year,
                            subject.Id.Kind,
                            subject.ContentKind,
                            _preferredLanguage,
                            subject.ExternalIds),
                        cancellationToken)
                    .ConfigureAwait(false);

                var previousArtwork = artwork;
                artwork = new Core.MetadataArtwork(
                    subject.Artwork.PosterUrl ??
                    artworkResult.Artwork.PosterUrl,
                    subject.Artwork.BackdropUrl ??
                    artworkResult.Artwork.BackdropUrl,
                    subject.Artwork.ThumbnailUrl ??
                    artworkResult.Artwork.ThumbnailUrl);
                artworkContributors =
                    artworkResult.Providers;

                if (string.IsNullOrWhiteSpace(previousArtwork.PosterUrl) &&
                    !string.IsNullOrWhiteSpace(artwork.PosterUrl) &&
                    artworkResult.Providers.Count > 0)
                {
                    mergeResult.FieldSources["PosterUrl"] =
                        string.Join("+", artworkResult.Providers);
                }

                if (string.IsNullOrWhiteSpace(previousArtwork.BackdropUrl) &&
                    !string.IsNullOrWhiteSpace(artwork.BackdropUrl) &&
                    artworkResult.Providers.Count > 0)
                {
                    mergeResult.FieldSources["BackdropUrl"] =
                        string.Join("+", artworkResult.Providers);
                }

                errors.AddRange(
                    artworkResult.ProviderErrors.Select(
                        static error =>
                            new MetadataProviderErrorSnapshot(
                                error.Provider,
                                error.ErrorType,
                                error.Message)));
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                errors.Add(
                    new MetadataProviderErrorSnapshot(
                        "artwork",
                        exception.GetType().Name,
                        exception.Message));
            }
        }

        var episodeThumbnailUrl = episode?.ThumbnailUrl;

        if (string.IsNullOrWhiteSpace(episodeThumbnailUrl) &&
            _tmdbProvider is not null)
        {
            var targetEpisodeNumber =
                episode?.EpisodeNumber ??
                recognition.EpisodeNumber ??
                recognition.SpecialNumber;

            if (targetEpisodeNumber is not null)
            {
                var tmdbCandidate = result.Resolution.Candidates
                    .Where(static candidate =>
                        string.Equals(
                            candidate.Candidate.Id.Provider,
                            "tmdb",
                            StringComparison.OrdinalIgnoreCase) &&
                        candidate.Candidate.Id.Kind ==
                            Core.MetadataSubjectKind.Series)
                    .Where(candidate =>
                        candidate.Score >= AutoResolveThreshold)
                    .OrderByDescending(static candidate =>
                        candidate.Score)
                    .ThenBy(static candidate =>
                        candidate.Candidate.ProviderRank)
                    .FirstOrDefault();

                if (tmdbCandidate is not null)
                {
                    var targetSeason =
                        episode?.SeasonNumber ??
                        recognition.SeasonNumber ??
                        (recognition.SpecialNumber is not null &&
                         recognition.EpisodeNumber is null
                            ? 0
                            : 1);

                    try
                    {
                        var tmdbEpisodes =
                            await _tmdbProvider.GetEpisodesAsync(
                                    tmdbCandidate.Candidate.Id,
                                    targetSeason,
                                    cancellationToken)
                                .ConfigureAwait(false);

                        var tmdbEpisode = tmdbEpisodes
                            .Where(item =>
                                item.EpisodeNumber ==
                                    targetEpisodeNumber)
                            .OrderBy(item =>
                                item.SeasonNumber ==
                                    targetSeason
                                    ? 0
                                    : 1)
                            .FirstOrDefault();

                        if (tmdbEpisode is not null)
                        {
                            episodeThumbnailUrl =
                                tmdbEpisode.ThumbnailUrl ??
                                episodeThumbnailUrl;
                            if (!string.IsNullOrWhiteSpace(
                                    tmdbEpisode.ProviderEpisodeId))
                            {
                                mergeResult.EpisodeExternalIds["tmdb"] =
                                    tmdbEpisode.ProviderEpisodeId;
                            }
                            if (!string.IsNullOrWhiteSpace(
                                    tmdbEpisode.ProviderSeasonId))
                            {
                                mergeResult.SeasonExternalIds["tmdb"] =
                                    tmdbEpisode.ProviderSeasonId;
                            }
                            if (!string.IsNullOrWhiteSpace(
                                    tmdbEpisode.ThumbnailUrl))
                            {
                                mergeResult.FieldSources[
                                    "EpisodeThumbnailUrl"] = "tmdb";
                            }
                        }
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        errors.Add(
                            new MetadataProviderErrorSnapshot(
                                "tmdb-episode-artwork",
                                exception.GetType().Name,
                                exception.Message));
                    }
                }
            }
        }

        return WithRouteDiagnostics(
            WithResolutionDiagnostics(
            new MediaMetadataSnapshot(
                RuntimeVersion,
                recognition.RuntimeVersion,
                MediaMetadataStatus.Resolved,
                subject.Id.Provider,
                subject.Id.Value,
                subject.Id.Kind.ToString(),
                subject.Titles.Primary,
                subject.Titles.Original,
                new Dictionary<string, string>(
                    subject.Titles.Localized,
                    StringComparer.OrdinalIgnoreCase),
                subject.Titles.Aliases.ToList(),
                subject.Overview,
                subject.ReleaseDate?.ToString("yyyy-MM-dd"),
                subject.EpisodeCount,
                artwork.PosterUrl,
                artwork.BackdropUrl,
                new Dictionary<string, string>(
                    subject.ExternalIds,
                    StringComparer.OrdinalIgnoreCase),
                episode?.EpisodeNumber ?? recognition.EpisodeNumber ?? recognition.SpecialNumber,
                episode?.Titles.Primary,
                episode?.Titles.Original,
                episode?.Overview,
                episode?.AirDate?.ToString("yyyy-MM-dd"),
                episodeThumbnailUrl,
                result.Resolution.Confidence,
                errors,
                DateTimeOffset.UtcNow)
            {
                EpisodeSeasonNumber =
                    episode?.SeasonNumber ??
                    recognition.SeasonNumber ??
                    (recognition.SpecialNumber is not null &&
                     recognition.EpisodeNumber is null
                        ? 0
                        : 1),
                ProviderEpisodeId = episode?.ProviderEpisodeId,
                ProviderSeasonId = episode?.ProviderSeasonId,
                SeasonTitle = episode?.SeasonTitle,
                SeasonOverview = episode?.SeasonOverview,
                SeasonAirDate = episode?.SeasonAirDate?.ToString("yyyy-MM-dd"),
                SeasonPosterUrl = episode?.SeasonPosterUrl,
                Genres = subject.Genres.ToList(),
                ProductionCompanies =
                    subject.ProductionCompanies.ToList(),
                OriginCountryCodes =
                    subject.OriginCountryCodes.ToList(),
                RuntimeMinutes = subject.RuntimeMinutes,
                ProductionStatus = subject.Status,
                OriginalLanguage = subject.OriginalLanguage,
                Cast = subject.Cast
                    .Select(static item =>
                        new MediaPersonCreditSnapshot(
                            item.ProviderPersonId,
                            item.Name,
                            item.Role,
                            item.Department,
                            item.ProfileUrl,
                            item.Order))
                    .ToList(),
                Crew = subject.Crew
                    .Select(static item =>
                        new MediaPersonCreditSnapshot(
                            item.ProviderPersonId,
                            item.Name,
                            item.Role,
                            item.Department,
                            item.ProfileUrl,
                            item.Order))
                    .ToList(),
                SeasonExternalIds =
                    new Dictionary<string, string>(
                        mergeResult.SeasonExternalIds,
                        StringComparer.OrdinalIgnoreCase),
                EpisodeExternalIds =
                    new Dictionary<string, string>(
                        mergeResult.EpisodeExternalIds,
                        StringComparer.OrdinalIgnoreCase),
                FieldSources =
                    new Dictionary<string, string>(
                        mergeResult.FieldSources,
                        StringComparer.OrdinalIgnoreCase),
                MergeContributors = mergeResult.Contributors
                    .Concat(artworkContributors)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                MergeProfile = mergeResult.Profile.ToString(),
                IdentityBindingProvider =
                    binding?.PrimaryProvider,
                IdentityBindingManual =
                    binding?.IsManual ?? false,
            },
            providerRequest,
            result.Resolution,
            subject),
            route);
    }

    public static MetadataRuntimeIdentity ProbeRuntime()
    {
        var coreAssembly = typeof(Core.MetadataResolver).Assembly;
        var providerAssembly = typeof(Provider.BangumiMetadataProvider).Assembly;
        var corePath = coreAssembly.Location;
        var providerPath = providerAssembly.Location;

        var coreVersion = ReadVersion(corePath, coreAssembly.GetName().Version);
        var providerVersion = ReadVersion(providerPath, providerAssembly.GetName().Version);

        var status = string.Equals(
            coreVersion,
            providerVersion,
            StringComparison.OrdinalIgnoreCase)
                ? "ok"
                : $"version-mismatch:{coreVersion}/{providerVersion}";

        return new MetadataRuntimeIdentity(
            coreVersion,
            corePath,
            providerPath,
            status);
    }

    private async Task<IReadOnlyList<Core.MetadataEnrichmentResult>>
        LoadSupplementalResultsAsync(
            Core.MetadataSearchRequest request,
            MediaProviderRoute route,
            string primaryProvider,
            MediaIdentityBindingHint? binding,
            CancellationToken cancellationToken)
    {
        var results =
            new List<Core.MetadataEnrichmentResult>();

        foreach (var provider in route.EnumerateProviders()
                     .Where(provider =>
                         !string.Equals(
                             provider,
                             primaryProvider,
                             StringComparison.OrdinalIgnoreCase)))
        {
            if (!_providerResolvers.TryGetValue(
                    provider,
                    out var resolver))
            {
                continue;
            }

            try
            {
                Core.MetadataEnrichmentResult candidate;
                if (binding is not null &&
                    binding.ExternalIds.TryGetValue(
                        provider,
                        out var boundId) &&
                    !string.IsNullOrWhiteSpace(boundId))
                {
                    candidate = await EnrichBoundProviderAsync(
                            provider,
                            boundId,
                            request,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    candidate = await resolver
                        .EnrichAsync(
                            request,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                if (candidate.Resolution.IsResolved &&
                    candidate.Subject is not null)
                {
                    results.Add(candidate);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Supplemental data must never invalidate
                // a successfully resolved primary provider.
            }
        }

        return results;
    }

    private async Task<Core.MetadataEnrichmentResult>
        EnrichBoundProviderAsync(
            string providerName,
            string providerSubjectId,
            Core.MetadataSearchRequest request,
            CancellationToken cancellationToken)
    {
        if (!_providers.TryGetValue(
                providerName,
                out var provider))
        {
            return new Core.MetadataEnrichmentResult(
                new Core.MetadataResolution(
                    Best: null,
                    IsResolved: false,
                    Confidence: 0,
                    Candidates: Array.Empty<Core.MetadataResolutionCandidate>(),
                    ProviderErrors:
                    [
                        new Core.MetadataProviderError(
                            providerName,
                            "ProviderUnavailable",
                            "Bound provider is not configured.")
                    ]),
                Subject: null,
                Episode: null,
                ProviderErrors:
                [
                    new Core.MetadataProviderError(
                        providerName,
                        "ProviderUnavailable",
                        "Bound provider is not configured.")
                ]);
        }

        var subjectKind =
            request.RecognitionMediaKind == RecognitionContracts.MediaKind.Movie
                ? Core.MetadataSubjectKind.Movie
                : Core.MetadataSubjectKind.Series;
        var id = new Core.MetadataProviderItemId(
            providerName,
            providerSubjectId,
            subjectKind);
        var subject = await provider
            .GetSubjectAsync(
                id,
                cancellationToken)
            .ConfigureAwait(false);

        if (subject is null)
        {
            return new Core.MetadataEnrichmentResult(
                new Core.MetadataResolution(
                    Best: null,
                    IsResolved: false,
                    Confidence: 0,
                    Candidates: Array.Empty<Core.MetadataResolutionCandidate>(),
                    ProviderErrors: Array.Empty<Core.MetadataProviderError>()),
                Subject: null,
                Episode: null,
                ProviderErrors: Array.Empty<Core.MetadataProviderError>());
        }

        Core.MetadataEpisode? episode = null;
        var targetEpisode = request.EpisodeNumber;
        if (subjectKind == Core.MetadataSubjectKind.Series &&
            targetEpisode is not null)
        {
            var season = request.SeasonNumber ?? 1;
            var episodes = await provider
                .GetEpisodesAsync(
                    id,
                    season,
                    cancellationToken)
                .ConfigureAwait(false);
            episode = episodes
                .Where(item =>
                    item.EpisodeNumber == targetEpisode)
                .OrderBy(item =>
                    item.SeasonNumber == season
                        ? 0
                        : 1)
                .FirstOrDefault();
        }

        var searchCandidate =
            new Core.MetadataSearchCandidate(
                id,
                subject.Titles,
                subject.ReleaseDate?.Year,
                ProviderRank: 0)
            {
                ContentKind = subject.ContentKind,
            };
        var best = new Core.MetadataResolutionCandidate(
            searchCandidate,
            Score: 1.0,
            Evidence:
            [
                "IdentityBinding",
            ]);
        var resolution = new Core.MetadataResolution(
            best,
            IsResolved: true,
            Confidence: 1.0,
            Candidates: [best],
            ProviderErrors: Array.Empty<Core.MetadataProviderError>())
        {
            Lead = 1.0,
        };

        return new Core.MetadataEnrichmentResult(
            resolution,
            subject,
            episode,
            Array.Empty<Core.MetadataProviderError>());
    }

    private async Task<Core.MetadataEnrichmentResult>
        EnrichWithRouteAsync(
            Core.MetadataSearchRequest request,
            MediaProviderRoute route,
            MediaIdentityBindingHint? binding,
            CancellationToken cancellationToken)
    {
        foreach (var provider in route.EnumerateProviders())
        {
            if (!_providerResolvers.TryGetValue(
                    provider,
                    out var resolver))
            {
                continue;
            }

            Core.MetadataEnrichmentResult candidate;
            if (binding is not null &&
                binding.ExternalIds.TryGetValue(
                    provider,
                    out var boundId) &&
                !string.IsNullOrWhiteSpace(boundId))
            {
                candidate = await EnrichBoundProviderAsync(
                        provider,
                        boundId,
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                candidate = await resolver
                    .EnrichAsync(
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            if (candidate.Resolution.IsResolved &&
                candidate.Resolution.Best is not null &&
                candidate.Subject is not null)
            {
                return candidate;
            }
        }

        return await _resolver!
            .EnrichAsync(
                request,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static MediaMetadataSnapshot WithRouteDiagnostics(
        MediaMetadataSnapshot snapshot,
        MediaProviderRoute route) =>
        snapshot with
        {
            RoutingPrimaryProvider =
                route.PrimaryProvider,
            RoutingFallbackProviders =
                route.FallbackProviders.ToList(),
            RoutingReason = route.Reason,
        };

    private static MediaMetadataSnapshot BuildFailure(
        HostRecognition.MediaRecognitionSnapshot recognition,
        string provider,
        string errorType,
        string message) =>
        new(
            RuntimeVersion,
            recognition.RuntimeVersion,
            MediaMetadataStatus.Error,
            Provider: null,
            ProviderSubjectId: null,
            SubjectKind: null,
            CanonicalTitle: null,
            OriginalTitle: null,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            [],
            Overview: null,
            ReleaseDate: null,
            EpisodeCount: null,
            PosterUrl: null,
            BackdropUrl: null,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            recognition.EpisodeNumber ?? recognition.SpecialNumber,
            EpisodeTitle: null,
            EpisodeOriginalTitle: null,
            EpisodeOverview: null,
            EpisodeAirDate: null,
            EpisodeThumbnailUrl: null,
            Confidence: 0,
            [new MetadataProviderErrorSnapshot(provider, errorType, message)],
            DateTimeOffset.UtcNow)
        {
            ResolutionReason = "Exception",
        };

    private static MediaMetadataSnapshot WithResolutionDiagnostics(
        MediaMetadataSnapshot snapshot,
        Core.MetadataSearchRequest providerRequest,
        Core.MetadataResolution resolution,
        Core.MetadataSubject? subject)
    {
        var best = resolution.Candidates.FirstOrDefault();
        var second = resolution.Candidates.Skip(1).FirstOrDefault();
        var lead = best is null
            ? (double?)null
            : second is null
                ? 1.0
                : best.Score - second.Score;

        return snapshot with
        {
            ResolutionReason = ResolutionReason(resolution, subject),
            ContentKind =
                ReadOptionalPropertyName(subject, "ContentKind") ??
                ReadOptionalPropertyName(
                    resolution.Best?.Candidate,
                    "ContentKind"),
            SearchTitles = providerRequest.Titles.ToList(),
            CandidateCount = resolution.Candidates.Count,
            AutoResolveThreshold = AutoResolveThreshold,
            MinimumLead = MinimumLead,
            BestScore = best?.Score,
            SecondScore = second?.Score,
            Lead = lead,
            TopCandidates = resolution.Candidates
                .Take(DiagnosticCandidateLimit)
                .Select(static candidate =>
                    new MetadataResolutionCandidateSnapshot(
                        candidate.Candidate.Id.Provider,
                        candidate.Candidate.Id.Value,
                        candidate.Candidate.Id.Kind.ToString(),
                        candidate.Candidate.Titles.Primary,
                        candidate.Candidate.Year,
                        candidate.Candidate.ProviderRank,
                        candidate.Score,
                        candidate.Evidence.ToList()))
                .ToList(),
        };
    }

    private static string? ReadOptionalPropertyName(
        object? source,
        string propertyName)
    {
        if (source is null)
        {
            return null;
        }

        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(source)?.ToString();
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value, "Unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private static string ResolutionReason(
        Core.MetadataResolution resolution,
        Core.MetadataSubject? subject)
    {
        if (resolution.Candidates.Count == 0)
        {
            return resolution.ProviderErrors.Count > 0
                ? "ProviderErrorNoCandidates"
                : "NoCandidates";
        }

        var best = resolution.Candidates[0];
        var second = resolution.Candidates.Skip(1).FirstOrDefault();

        if (best.Score < AutoResolveThreshold)
        {
            return "BelowAutoResolveThreshold";
        }

        if (second is not null &&
            best.Score - second.Score < MinimumLead)
        {
            return "InsufficientLead";
        }

        if (!resolution.IsResolved)
        {
            return "Unresolved";
        }

        return subject is null
            ? "SubjectUnavailable"
            : "Resolved";
    }

    private static RecognitionContracts.MediaKind ParseMediaKind(string value) =>
        Enum.TryParse<RecognitionContracts.MediaKind>(
            value,
            ignoreCase: true,
            out var parsed)
            ? parsed
            : RecognitionContracts.MediaKind.Unknown;

    private static void AddTitle(ICollection<string> target, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        if (target.Any(existing =>
                string.Equals(
                    existing,
                    title,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        target.Add(title);
    }

    private static string ReadVersion(
        string assemblyPath,
        Version? fallback)
    {
        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            var fileVersion =
                FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;
            if (!string.IsNullOrWhiteSpace(fileVersion) &&
                Version.TryParse(fileVersion, out var parsed))
            {
                return $"{parsed.Major}.{Math.Max(0, parsed.Minor)}.{Math.Max(0, parsed.Build)}";
            }
        }

        fallback ??= new Version(0, 0, 0);
        return $"{fallback.Major}.{Math.Max(0, fallback.Minor)}.{Math.Max(0, fallback.Build)}";
    }

    private static HttpClient CreateHttpClient() =>
        new()
        {
            Timeout = TimeSpan.FromSeconds(12),
        };
}
