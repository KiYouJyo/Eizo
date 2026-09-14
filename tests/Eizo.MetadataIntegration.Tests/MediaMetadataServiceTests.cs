using System.Net;
using System.Text;
using Eizo.MetadataIntegration;
using Eizo.Recognition;

namespace Eizo.MetadataIntegration.Tests;

public sealed class MediaMetadataServiceTests
{
    [Fact]
    public async Task EnrichAsync_MapsRecognitionIntoBangumiMetadata()
    {
        using var cache = new TempDirectory();
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                    "/v0/search/subjects",
                    StringComparison.Ordinal))
            {
                return Json("""
                {
                  "data": [
                    {
                      "id": 253,
                      "type": 2,
                      "name": "攻殻機動隊 STAND ALONE COMPLEX",
                      "name_cn": "攻壳机动队 STAND ALONE COMPLEX",
                      "date": "2002-10-01",
                      "platform": "TV",
                      "rating": { "score": 8.8 }
                    }
                  ],
                  "total": 1
                }
                """);
            }

            if (request.RequestUri.AbsolutePath.EndsWith(
                    "/v0/subjects/253",
                    StringComparison.Ordinal))
            {
                return Json("""
                {
                  "id": 253,
                  "type": 2,
                  "name": "攻殻機動隊 STAND ALONE COMPLEX",
                  "name_cn": "攻壳机动队 STAND ALONE COMPLEX",
                  "date": "2002-10-01",
                  "platform": "TV",
                  "summary": "Section 9 investigates cybercrime.",
                  "eps": 26,
                  "images": {
                    "large": "https://example.test/poster.jpg"
                  }
                }
                """);
            }

            if (request.RequestUri.AbsolutePath.EndsWith(
                    "/v0/episodes",
                    StringComparison.Ordinal))
            {
                return Json("""
                {
                  "data": [
                    {
                      "id": 1001,
                      "type": 0,
                      "sort": 1,
                      "name": "公安9課",
                      "name_cn": "公安九课",
                      "airdate": "2002-10-01"
                    }
                  ],
                  "total": 1
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new MediaMetadataService(
            new MediaMetadataServiceOptions(
                CacheDirectory: cache.Path),
            new HttpClient(handler));

        var result = await service.EnrichAsync(
            Recognition(
                title: "攻殻機動隊 STAND ALONE COMPLEX",
                year: 2002,
                episode: 1),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(MediaMetadataStatus.Resolved, result.Status);
        Assert.Equal("bangumi", result.Provider);
        Assert.Equal("253", result.ProviderSubjectId);
        Assert.Equal("攻壳机动队 STAND ALONE COMPLEX", result.CanonicalTitle);
        Assert.Equal("公安九课", result.EpisodeTitle);
        Assert.Equal(1m, result.EpisodeNumber);
        Assert.Equal("Resolved", result.ResolutionReason);
        Assert.Equal("Animation", result.ContentKind);
        Assert.Equal(1, result.CandidateCount);
        Assert.NotNull(result.BestScore);
        Assert.True(result.BestScore >= result.AutoResolveThreshold);
        Assert.Contains("攻殻機動隊 STAND ALONE COMPLEX", result.SearchTitles);
    }

    [Fact]
    public async Task EnrichAsync_DoesNotCallAniListWhenRoadmapSkipsIt()
    {
        using var cache = new TempDirectory();
        var aniListCalls = 0;

        var bangumiHandler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith(
                    "/v0/search/subjects",
                    StringComparison.Ordinal))
            {
                return Json("""
                {
                  "data": [
                    {
                      "id": 400,
                      "type": 2,
                      "name": "葬送のフリーレン",
                      "name_cn": "葬送的芙莉莲",
                      "date": "2023-09-29",
                      "platform": "TV",
                      "rating": { "score": 9.0 }
                    }
                  ],
                  "total": 1
                }
                """);
            }

            if (path.EndsWith(
                    "/v0/subjects/400",
                    StringComparison.Ordinal))
            {
                return Json("""
                {
                  "id": 400,
                  "type": 2,
                  "name": "葬送のフリーレン",
                  "name_cn": "葬送的芙莉莲",
                  "date": "2023-09-29",
                  "platform": "TV",
                  "summary": "Journey.",
                  "eps": 28,
                  "images": {
                    "large": "https://example.test/frieren-poster.jpg"
                  }
                }
                """);
            }

            if (path.EndsWith(
                    "/v0/episodes",
                    StringComparison.Ordinal))
            {
                return Json("""
                {
                  "data": [
                    {
                      "id": 1,
                      "type": 0,
                      "sort": 1,
                      "name": "冒険の終わり",
                      "airdate": "2023-09-29"
                    }
                  ],
                  "total": 1
                }
                """);
            }

            return new HttpResponseMessage(
                HttpStatusCode.NotFound);
        });

        var aniListHandler = new RecordingHandler(_ =>
        {
            aniListCalls++;
            return Json("""{"data":{"Page":{"media":[]}}}""");
        });

        var service = new MediaMetadataService(
            new MediaMetadataServiceOptions(
                CacheDirectory: cache.Path),
            bangumiHttpClient:
                new HttpClient(bangumiHandler),
            anilistHttpClient:
                new HttpClient(aniListHandler));

        var result = await service.EnrichAsync(
            Recognition(
                "葬送のフリーレン",
                year: 2023,
                episode: 1),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(
            MediaMetadataStatus.Resolved,
            result.Status);
        Assert.Equal(0, aniListCalls);
        Assert.Null(result.BackdropUrl);
        Assert.Equal(
            "https://example.test/frieren-poster.jpg",
            result.PosterUrl);
    }

    [Fact]
    public async Task EnrichAsync_ReusesRuntimeScopedPersistentCacheAcrossServiceInstances()
    {
        using var cache = new TempDirectory();
        var searchCalls = 0;
        var subjectCalls = 0;
        var episodeCalls = 0;

        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/v0/search/subjects", StringComparison.Ordinal))
            {
                searchCalls++;
                return Json("""
                {
                  "data": [
                    {
                      "id": 253,
                      "name": "攻殻機動隊 STAND ALONE COMPLEX",
                      "name_cn": "攻壳机动队 STAND ALONE COMPLEX",
                      "date": "2002-10-01",
                      "platform": "TV"
                    }
                  ],
                  "total": 1
                }
                """);
            }

            if (path.EndsWith("/v0/subjects/253", StringComparison.Ordinal))
            {
                subjectCalls++;
                return Json("""
                {
                  "id": 253,
                  "name": "攻殻機動隊 STAND ALONE COMPLEX",
                  "name_cn": "攻壳机动队 STAND ALONE COMPLEX",
                  "date": "2002-10-01",
                  "platform": "TV",
                  "eps": 26
                }
                """);
            }

            if (path.EndsWith("/v0/episodes", StringComparison.Ordinal))
            {
                episodeCalls++;
                return Json("""
                {
                  "data": [
                    { "id": 1001, "type": 0, "sort": 1, "name": "EP1" },
                    { "id": 1002, "type": 0, "sort": 2, "name": "EP2" }
                  ],
                  "total": 2
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var firstService = new MediaMetadataService(
            new MediaMetadataServiceOptions(CacheDirectory: cache.Path),
            new HttpClient(handler));

        _ = await firstService.EnrichAsync(
            Recognition("攻殻機動隊 STAND ALONE COMPLEX", 2002, 1),
            TestContext.Current.CancellationToken);

        var runtimeCacheRoot = Path.Combine(
            cache.Path,
            $"runtime-{MediaMetadataService.RuntimeVersion}");
        Assert.True(Directory.Exists(runtimeCacheRoot));

        // A new service instance has a fresh memory cache. If the second call
        // does not hit the network, reuse is coming from the runtime-scoped
        // persistent cache rather than process-local state.
        var secondService = new MediaMetadataService(
            new MediaMetadataServiceOptions(CacheDirectory: cache.Path),
            new HttpClient(handler));

        _ = await secondService.EnrichAsync(
            Recognition("攻殻機動隊 STAND ALONE COMPLEX", 2002, 2),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, searchCalls);
        // Metadata 0.2.20 enriches the leading Bangumi search candidate with
        // one subject-detail request before the resolver fetches the selected
        // subject. The second episode must still reuse both cached results.
        Assert.Equal(2, subjectCalls);
        Assert.Equal(1, episodeCalls);
    }

    [Fact]
    public async Task EnrichAsync_ReportsNoCandidatesAndEffectiveSearchTitles()
    {
        using var cache = new TempDirectory();
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                    "/v0/search/subjects",
                    StringComparison.Ordinal))
            {
                return Json("""{"data":[],"total":0}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new MediaMetadataService(
            new MediaMetadataServiceOptions(
                CacheDirectory: cache.Path),
            new HttpClient(handler));

        var result = await service.EnrichAsync(
            Recognition(
                "S01 攻壳机动队 STAND ALONE COMPLEX",
                year: 2002,
                episode: 1),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(MediaMetadataStatus.Unresolved, result.Status);
        Assert.Equal("NoCandidates", result.ResolutionReason);
        Assert.Equal(0, result.CandidateCount);
        Assert.Null(result.BestScore);
        Assert.Contains(
            "S01 攻壳机动队 STAND ALONE COMPLEX",
            result.SearchTitles);
        Assert.Contains(
            "攻壳机动队 STAND ALONE COMPLEX",
            result.SearchTitles);
        Assert.Equal(0.82, result.AutoResolveThreshold, precision: 3);
        Assert.Equal(0.06, result.MinimumLead, precision: 3);
    }

    [Fact]
    public async Task EnrichAsync_UsesBestCandidateContentKindWhenResolutionIsAmbiguous()
    {
        using var cache = new TempDirectory();
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsolutePath.EndsWith(
                    "/v0/search/subjects",
                    StringComparison.Ordinal))
            {
                return Json("""
                {
                  "data": [
                    {
                      "id": 100,
                      "type": 2,
                      "name": "Example Anime",
                      "name_cn": "示例动画",
                      "date": "2024-01-01",
                      "platform": "TV"
                    },
                    {
                      "id": 101,
                      "type": 2,
                      "name": "Example Anime",
                      "name_cn": "示例动画",
                      "date": "2024-01-01",
                      "platform": "TV"
                    }
                  ],
                  "total": 2
                }
                """);
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsolutePath.Contains(
                    "/v0/subjects/",
                    StringComparison.Ordinal) &&
                !request.RequestUri.AbsolutePath.EndsWith(
                    "/subjects",
                    StringComparison.Ordinal))
            {
                return Json("""
                {
                  "id": 100,
                  "type": 2,
                  "name": "Example Anime",
                  "name_cn": "示例动画",
                  "date": "2024-01-01",
                  "platform": "TV",
                  "eps": 12
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new MediaMetadataService(
            new MediaMetadataServiceOptions(
                CacheDirectory: cache.Path),
            new HttpClient(handler));

        var result = await service.EnrichAsync(
            Recognition(
                "示例动画",
                year: 2024,
                episode: 1),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(MediaMetadataStatus.Unresolved, result.Status);
        Assert.Equal("InsufficientLead", result.ResolutionReason);
        Assert.Equal("Animation", result.ContentKind);
        Assert.True(result.CandidateCount >= 2);
    }

    [Fact]
    public async Task EnrichAsync_TmdbCarriesExactSeriesEpisodeIdentityAndStill()
    {
        using var cache = new TempDirectory();
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/search/tv", StringComparison.Ordinal))
            {
                return Json("""
                {
                  "results": [
                    {
                      "id": 1396,
                      "name": "Breaking Bad",
                      "original_name": "Breaking Bad",
                      "first_air_date": "2008-01-20",
                      "popularity": 100.0,
                      "genre_ids": [18]
                    }
                  ]
                }
                """);
            }

            if (path.EndsWith("/tv/1396", StringComparison.Ordinal))
            {
                return Json("""
                {
                  "id": 1396,
                  "name": "Breaking Bad",
                  "original_name": "Breaking Bad",
                  "overview": "A chemistry teacher turns to crime.",
                  "first_air_date": "2008-01-20",
                  "number_of_episodes": 62,
                  "poster_path": "/poster.jpg",
                  "backdrop_path": "/backdrop.jpg",
                  "episode_run_time": [47],
                  "status": "Ended",
                  "original_language": "en",
                  "origin_country": ["US"],
                  "genres": [{"id": 18, "name": "Drama"}],
                  "production_companies": [
                    {"id": 2605, "name": "High Bridge Productions"}
                  ],
                  "credits": {
                    "cast": [
                      {
                        "id": 17419,
                        "name": "Bryan Cranston",
                        "character": "Walter White",
                        "order": 0,
                        "profile_path": "/cranston.jpg"
                      }
                    ],
                    "crew": [
                      {
                        "id": 66633,
                        "name": "Vince Gilligan",
                        "job": "Executive Producer",
                        "department": "Production",
                        "profile_path": "/gilligan.jpg"
                      }
                    ]
                  },
                  "external_ids": {
                    "imdb_id": "tt0903747",
                    "tvdb_id": 81189
                  }
                }
                """);
            }

            if (path.EndsWith("/tv/1396/season/1", StringComparison.Ordinal))
            {
                return Json("""
                {
                  "id": 3572,
                  "name": "Season 1",
                  "overview": "The first season.",
                  "air_date": "2008-01-20",
                  "poster_path": "/season1.jpg",
                  "season_number": 1,
                  "episodes": [
                    {
                      "id": 62085,
                      "episode_number": 1,
                      "name": "Pilot",
                      "overview": "Walter White begins his transformation.",
                      "air_date": "2008-01-20",
                      "still_path": "/pilot.jpg"
                    }
                  ]
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new MediaMetadataService(
            new MediaMetadataServiceOptions(
                EnableBangumi: false,
                TmdbReadAccessToken: "test-token",
                CacheDirectory: cache.Path,
                EnableArtworkProviders: false),
            bangumiHttpClient: null,
            tmdbHttpClient: new HttpClient(handler));

        var result = await service.EnrichAsync(
            Recognition("Breaking Bad", 2008, 1),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(MediaMetadataStatus.Resolved, result.Status);
        Assert.Equal("tmdb", result.Provider);
        Assert.Equal("1396", result.ProviderSubjectId);
        Assert.Equal("3572", result.ProviderSeasonId);
        Assert.Equal("62085", result.ProviderEpisodeId);
        Assert.Equal(1, result.EpisodeSeasonNumber);
        Assert.Equal("Season 1", result.SeasonTitle);
        Assert.Equal("The first season.", result.SeasonOverview);
        Assert.Equal("2008-01-20", result.SeasonAirDate);
        Assert.EndsWith("/season1.jpg", result.SeasonPosterUrl);
        Assert.Equal(1m, result.EpisodeNumber);
        Assert.Equal("Pilot", result.EpisodeTitle);
        Assert.EndsWith("/pilot.jpg", result.EpisodeThumbnailUrl);
        Assert.Equal("1396", result.ExternalIds["tmdb"]);
        Assert.Equal("tt0903747", result.ExternalIds["imdb"]);
        Assert.Equal("Drama", Assert.Single(result.Genres));
        Assert.Equal(
            "High Bridge Productions",
            Assert.Single(result.ProductionCompanies));
        Assert.Equal("US", Assert.Single(result.OriginCountryCodes));
        Assert.Equal(47, result.RuntimeMinutes);
        Assert.Equal("Ended", result.ProductionStatus);
        Assert.Equal("en", result.OriginalLanguage);

        var cast = Assert.Single(result.Cast);
        Assert.Equal("Bryan Cranston", cast.Name);
        Assert.Equal("Walter White", cast.Role);
        Assert.EndsWith("/cranston.jpg", cast.ProfileUrl);

        var crew = Assert.Single(result.Crew);
        Assert.Equal("Vince Gilligan", crew.Name);
        Assert.Equal("Executive Producer", crew.Role);
        Assert.Equal("Production", crew.Department);
    }

    [Fact]
    public async Task EnrichAsync_DoesNotCallNetworkForAmbiguousRecognition()
    {
        using var cache = new TempDirectory();
        var calls = 0;
        var handler = new RecordingHandler(_ =>
        {
            calls++;
            return Json("""{"data":[],"total":0}""");
        });

        var service = new MediaMetadataService(
            new MediaMetadataServiceOptions(
                CacheDirectory: cache.Path),
            new HttpClient(handler));

        var recognition = Recognition(
            "Conflicting Show",
            year: null,
            episode: 1) with
        {
            Status = MediaRecognitionStatus.Ambiguous,
        };

        var result = await service.EnrichAsync(
            recognition,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, calls);
    }

    private static MediaRecognitionSnapshot Recognition(
        string title,
        int? year,
        decimal? episode) =>
        new(
            LogicalPath: $"{title} - {episode}.mkv",
            Status: MediaRecognitionStatus.Recognized,
            MediaKind: "SeriesEpisode",
            SpecialKind: "None",
            EpisodePart: "None",
            IsFinalEpisode: false,
            Title: title,
            EpisodeTitle: null,
            TitleCandidates:
            [
                new RecognitionTitleCandidateSnapshot(
                    title,
                    0.95,
                    "filename",
                    true),
            ],
            SeasonNumber: 1,
            CourNumber: null,
            EpisodeNumber: episode,
            EpisodeEndNumber: null,
            SpecialNumber: null,
            Year: year,
            Confidence: 0.95,
            ConfidenceLevel: "High",
            IsAmbiguous: false,
            Evidence: [])
        {
            RuntimeVersion = "0.1.6",
        };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                body,
                Encoding.UTF8,
                "application/json"),
        };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "Eizo.MetadataIntegration.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
