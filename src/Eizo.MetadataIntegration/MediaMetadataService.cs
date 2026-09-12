using System.Diagnostics;
using Core = Eizo.Metadata.Core;
using Provider = Eizo.Metadata.Providers;
using RecognitionContracts = Eizo.Metadata.Recognition;
using HostRecognition = Eizo.Recognition;

namespace Eizo.MetadataIntegration;

public sealed record MediaMetadataServiceOptions(
    bool EnableBangumi = true,
    string BangumiUserAgent = "KiYouJyo/Eizo/0.3.9 (https://github.com/KiYouJyo/Eizo)",
    string PreferredLanguage = "zh-CN",
    string? TmdbReadAccessToken = null,
    string? CacheDirectory = null);

public sealed class MediaMetadataService
{
    private const double AutoResolveThreshold = 0.82;
    private const double MinimumLead = 0.06;
    private const int DiagnosticCandidateLimit = 5;

    private static readonly HttpClient SharedBangumiHttpClient = CreateHttpClient();
    private static readonly HttpClient SharedTmdbHttpClient = CreateHttpClient();

    private readonly Core.MetadataResolver? _resolver;
    private readonly string _preferredLanguage;

    public MediaMetadataService(
        MediaMetadataServiceOptions? options = null,
        HttpClient? bangumiHttpClient = null,
        HttpClient? tmdbHttpClient = null)
    {
        options ??= new MediaMetadataServiceOptions();
        _preferredLanguage = string.IsNullOrWhiteSpace(options.PreferredLanguage)
            ? "zh-CN"
            : options.PreferredLanguage;

        var cacheRoot = string.IsNullOrWhiteSpace(options.CacheDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Eizo",
                "MetadataCache")
            : Path.GetFullPath(options.CacheDirectory);

        var fileCache = new Core.FileMetadataCache(cacheRoot);
        var memoryCache = new Core.MemoryMetadataCache();
        var providers = new List<Core.IMetadataProvider>();

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

            providers.Add(new Core.CachedMetadataProvider(
                persistentBangumi,
                memoryCache,
                Core.MetadataCachePolicy.Default));
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

            providers.Add(new Core.CachedMetadataProvider(
                persistentTmdb,
                memoryCache,
                Core.MetadataCachePolicy.Default));
        }

        _resolver = providers.Count == 0
            ? null
            : new Core.MetadataResolver(
                providers,
                new Core.MetadataResolverOptions(
                    AutoResolveThreshold,
                    MinimumLead));
    }

    public static string RuntimeVersion => ProbeRuntime().Version;

    public bool IsAvailable => _resolver is not null;

    public async Task<MediaMetadataSnapshot?> EnrichAsync(
        HostRecognition.MediaRecognitionSnapshot recognition,
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
        try
        {
            result = await _resolver
                .EnrichAsync(request, cancellationToken)
                .ConfigureAwait(false);
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
            return WithResolutionDiagnostics(
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
                result.Subject);
        }

        var subject = result.Subject;
        var episode = result.Episode;
        return WithResolutionDiagnostics(
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
                subject.Artwork.PosterUrl,
                subject.Artwork.BackdropUrl,
                new Dictionary<string, string>(
                    subject.ExternalIds,
                    StringComparer.OrdinalIgnoreCase),
                episode?.EpisodeNumber ?? recognition.EpisodeNumber ?? recognition.SpecialNumber,
                episode?.Titles.Primary,
                episode?.Titles.Original,
                episode?.Overview,
                episode?.AirDate?.ToString("yyyy-MM-dd"),
                episode?.ThumbnailUrl,
                result.Resolution.Confidence,
                errors,
                DateTimeOffset.UtcNow),
            providerRequest,
            result.Resolution,
            subject);
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
