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
    public async Task EnrichAsync_ReusesSubjectAndEpisodeCachesAcrossEpisodes()
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

        var service = new MediaMetadataService(
            new MediaMetadataServiceOptions(CacheDirectory: cache.Path),
            new HttpClient(handler));

        _ = await service.EnrichAsync(
            Recognition("攻殻機動隊 STAND ALONE COMPLEX", 2002, 1),
            TestContext.Current.CancellationToken);
        _ = await service.EnrichAsync(
            Recognition("攻殻機動隊 STAND ALONE COMPLEX", 2002, 2),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, searchCalls);
        Assert.Equal(1, subjectCalls);
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
                var id = request.RequestUri.AbsolutePath
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Last();
                return Json($"""
                {
                  "id": {{id}},
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
