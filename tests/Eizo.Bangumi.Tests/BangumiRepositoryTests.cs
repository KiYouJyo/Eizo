using System.Net;
using System.Text;
using Eizo.Bangumi;

namespace Eizo.Bangumi.Tests;

public sealed class BangumiRepositoryTests
{
    [Fact]
    public async Task SeasonFetch_CombinesAllThreeQuarterMonths()
    {
        var requested = new List<string>();
        var handler = new CallbackHandler(request =>
        {
            var uri = request.RequestUri!.ToString();
            lock (requested)
                requested.Add(uri);

            var month = uri.Contains("month=7", StringComparison.Ordinal)
                ? 7
                : uri.Contains("month=8", StringComparison.Ordinal)
                    ? 8
                    : 9;

            return JsonResponse($$"""
            {
              "total": 1,
              "limit": 50,
              "offset": 0,
              "data": [
                {
                  "id": {{month}},
                  "type": 2,
                  "name": "Anime {{month}}",
                  "name_cn": "动画 {{month}}",
                  "summary": "",
                  "date": "2026-{{month:00}}-01",
                  "platform": "TV",
                  "images": {
                    "large": "",
                    "common": "",
                    "medium": "",
                    "small": "",
                    "grid": ""
                  },
                  "eps": 12,
                  "total_episodes": 12,
                  "rating": {
                    "rank": {{month}},
                    "total": 1,
                    "count": {},
                    "score": 7.0
                  },
                  "collection": {
                    "wish": 0,
                    "collect": 0,
                    "doing": 0,
                    "on_hold": 0,
                    "dropped": 0
                  },
                  "meta_tags": [],
                  "tags": []
                }
              ]
            }
            """);
        });

        var cacheRoot = CreateTempDirectory();
        try
        {
            using var client = CreateClient(handler);
            var repository = new BangumiRepository(
                new BangumiApiClient(client),
                new BangumiCacheStore(cacheRoot));

            var result = await repository.GetSeasonAsync(
                2026,
                7,
                forceRefresh: true);

            Assert.Equal(3, result.Value.Items.Count);
            Assert.Contains(
                requested,
                value => value.Contains("month=7", StringComparison.Ordinal));
            Assert.Contains(
                requested,
                value => value.Contains("month=8", StringComparison.Ordinal));
            Assert.Contains(
                requested,
                value => value.Contains("month=9", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CalendarRefresh_FallsBackToStaleCache()
    {
        var requestCount = 0;
        var handler = new CallbackHandler(_ =>
        {
            requestCount++;
            if (requestCount > 1)
            {
                return new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable);
            }

            return JsonResponse("""
            [
              {
                "weekday": {
                  "en": "Mon",
                  "cn": "星期一",
                  "ja": "月曜日",
                  "id": 1
                },
                "items": [
                  {
                    "id": 10,
                    "type": 2,
                    "name": "Anime",
                    "name_cn": "动画",
                    "summary": "",
                    "air_date": "2026-07-06",
                    "images": {
                      "large": "https://lain.bgm.tv/a.jpg"
                    },
                    "eps": 12,
                    "eps_count": 12,
                    "rating": {
                      "score": 7.5
                    },
                    "rank": 400,
                    "collection": {
                      "wish": 0,
                      "collect": 0,
                      "doing": 0,
                      "on_hold": 0,
                      "dropped": 0
                    }
                  }
                ]
              }
            ]
            """);
        });

        var cacheRoot = CreateTempDirectory();
        try
        {
            using var client = CreateClient(handler);
            var repository = new BangumiRepository(
                new BangumiApiClient(client),
                new BangumiCacheStore(cacheRoot));

            var first = await repository.GetCalendarAsync(
                forceRefresh: true);
            Assert.False(first.IsFromCache);
            Assert.False(first.IsStale);

            var fallback = await repository.GetCalendarAsync(
                forceRefresh: true);

            Assert.True(fallback.IsFromCache);
            Assert.True(fallback.IsStale);
            Assert.Single(fallback.Value.Days);
            Assert.Single(fallback.Value.Days[0].Items);
        }
        finally
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

    private static HttpClient CreateClient(
        HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri(
                "https://api.bgm.tv/",
                UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(5),
        };

    private static HttpResponseMessage JsonResponse(
        string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "Eizo-Bangumi-Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, HttpResponseMessage> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(callback(request));
    }
    [Fact]
    public async Task AccountReads_SendBearerTokenAndWatchingFilter()
    {
        const string token = "secret-test-token";
        var requested = new List<HttpRequestMessage>();

        var handler = new CallbackHandler(request =>
        {
            var clone = new HttpRequestMessage(
                request.Method,
                request.RequestUri);
            clone.Headers.Authorization =
                request.Headers.Authorization;
            requested.Add(clone);

            if (request.RequestUri!.AbsolutePath.EndsWith(
                    "/v0/me",
                    StringComparison.Ordinal))
            {
                return JsonResponse("""
                {
                  "id": 42,
                  "username": "eizo-user",
                  "nickname": "Eizo User",
                  "user_group": 10,
                  "avatar": {
                    "large": "",
                    "medium": "",
                    "small": ""
                  },
                  "sign": ""
                }
                """);
            }

            return JsonResponse("""
            {
              "total": 0,
              "limit": 50,
              "offset": 0,
              "data": []
            }
            """);
        });

        var cacheRoot = CreateTempDirectory();
        try
        {
            using var client = CreateClient(handler);
            var repository = new BangumiRepository(
                new BangumiApiClient(client),
                new BangumiCacheStore(cacheRoot));

            var profile =
                await repository.GetMyselfAsync(
                    token,
                    TestContext.Current.CancellationToken);
            var following =
                await repository.GetFollowingAsync(
                    token,
                    profile.UserName,
                    cancellationToken:
                        TestContext.Current.CancellationToken);

            Assert.Equal("eizo-user", profile.UserName);
            Assert.Empty(following.Items);
            Assert.Equal(2, requested.Count);

            foreach (var request in requested)
            {
                Assert.Equal(
                    "Bearer",
                    request.Headers.Authorization?.Scheme);
                Assert.Equal(
                    token,
                    request.Headers.Authorization?.Parameter);
            }

            var collectionUri =
                requested[1].RequestUri!.ToString();
            Assert.Contains(
                "subject_type=2",
                collectionUri,
                StringComparison.Ordinal);
            Assert.Contains(
                "type=3",
                collectionUri,
                StringComparison.Ordinal);
            Assert.Contains(
                "limit=50",
                collectionUri,
                StringComparison.Ordinal);
        }
        finally
        {
            foreach (var request in requested)
                request.Dispose();

            Directory.Delete(
                cacheRoot,
                recursive: true);
        }
    }

    [Fact]
    public async Task UserCollection_ReadsRequestedCollectionType()
    {
        const string token = "secret-test-token";
        string? collectionUri = null;

        var handler = new CallbackHandler(request =>
        {
            collectionUri = request.RequestUri!.ToString();
            return JsonResponse("""
            {
              "total": 0,
              "limit": 50,
              "offset": 0,
              "data": []
            }
            """);
        });

        var cacheRoot = CreateTempDirectory();
        try
        {
            using var client = CreateClient(handler);
            var repository = new BangumiRepository(
                new BangumiApiClient(client),
                new BangumiCacheStore(cacheRoot));

            var result = await repository.GetUserCollectionAsync(
                token,
                "eizo-user",
                BangumiCollectionType.Dropped,
                cancellationToken:
                    TestContext.Current.CancellationToken);

            Assert.Empty(result.Items);
            Assert.Contains(
                "subject_type=2",
                collectionUri,
                StringComparison.Ordinal);
            Assert.Contains(
                "type=5",
                collectionUri,
                StringComparison.Ordinal);
            Assert.Contains(
                "limit=50",
                collectionUri,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(
                cacheRoot,
                recursive: true);
        }
    }

    [Fact]
    public async Task SearchAnime_UsesGlobalSubjectSearchAndAnimeFilter()
    {
        HttpMethod? method = null;
        string? requestUri = null;
        string? requestBody = null;

        var handler = new CallbackHandler(request =>
        {
            method = request.Method;
            requestUri = request.RequestUri!.ToString();
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync()
                    .GetAwaiter()
                    .GetResult();

            return JsonResponse("""
            {
              "total": 1,
              "limit": 10,
              "offset": 0,
              "data": [
                {
                  "id": 123,
                  "type": 2,
                  "name": "Sousou no Frieren",
                  "name_cn": "葬送的芙莉莲",
                  "summary": "",
                  "date": "2023-09-29",
                  "platform": "TV",
                  "images": {
                    "large": "https://lain.bgm.tv/frieren.jpg"
                  },
                  "eps": 28,
                  "total_episodes": 28,
                  "rating": {
                    "rank": 10,
                    "score": 9.0
                  },
                  "collection": {
                    "wish": 1,
                    "collect": 2,
                    "doing": 3,
                    "on_hold": 0,
                    "dropped": 0
                  }
                }
              ]
            }
            """);
        });

        var cacheRoot = CreateTempDirectory();
        try
        {
            using var client = CreateClient(handler);
            var repository = new BangumiRepository(
                new BangumiApiClient(client),
                new BangumiCacheStore(cacheRoot));

            var result = await repository.SearchAnimeAsync(
                "芙莉莲",
                limit: 10,
                cancellationToken:
                    TestContext.Current.CancellationToken);

            Assert.Equal(HttpMethod.Post, method);
            Assert.Contains(
                "/v0/search/subjects?limit=10&offset=0",
                requestUri,
                StringComparison.Ordinal);

            using var requestJson =
                System.Text.Json.JsonDocument.Parse(
                    requestBody!);
            var root = requestJson.RootElement;

            Assert.Equal(
                "芙莉莲",
                root.GetProperty("keyword").GetString());
            Assert.Equal(
                2,
                root.GetProperty("filter")
                    .GetProperty("type")[0]
                    .GetInt32());

            Assert.Single(result.Items);
            Assert.Equal(123, result.Items[0].Id);
        }
        finally
        {
            Directory.Delete(
                cacheRoot,
                recursive: true);
        }
    }


    [Fact]
    public async Task SubjectCollection_ReadsBearerTokenAndCurrentType()
    {
        const string token = "secret-test-token";
        HttpMethod? method = null;
        string? requestUri = null;
        string? authScheme = null;
        string? authToken = null;

        var handler = new CallbackHandler(request =>
        {
            method = request.Method;
            requestUri = request.RequestUri!.ToString();
            authScheme = request.Headers.Authorization?.Scheme;
            authToken = request.Headers.Authorization?.Parameter;

            return JsonResponse("""
            {
              "subject_id": 123,
              "subject_type": 2,
              "rate": 8,
              "type": 3,
              "ep_status": 4,
              "vol_status": 0,
              "updated_at": "2026-09-18T12:00:00+08:00",
              "private": false
            }
            """);
        });

        var cacheRoot = CreateTempDirectory();
        try
        {
            using var client = CreateClient(handler);
            var repository = new BangumiRepository(
                new BangumiApiClient(client),
                new BangumiCacheStore(cacheRoot));

            var result =
                await repository.GetUserSubjectCollectionAsync(
                    token,
                    "eizo-user",
                    123,
                    TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(
                BangumiCollectionType.Doing,
                result.Type);
            Assert.Equal(HttpMethod.Get, method);
            Assert.Contains(
                "/v0/users/eizo-user/collections/123",
                requestUri,
                StringComparison.Ordinal);
            Assert.Equal("Bearer", authScheme);
            Assert.Equal(token, authToken);
        }
        finally
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SubjectCollection_ReturnsNullWhenNotCollected()
    {
        var handler = new CallbackHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        var cacheRoot = CreateTempDirectory();
        try
        {
            using var client = CreateClient(handler);
            var repository = new BangumiRepository(
                new BangumiApiClient(client),
                new BangumiCacheStore(cacheRoot));

            var result =
                await repository.GetUserSubjectCollectionAsync(
                    "secret-test-token",
                    "eizo-user",
                    123,
                    TestContext.Current.CancellationToken);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SetSubjectCollectionType_ExposesBangumiErrorDetail()
    {
        var handler = new CallbackHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    """
                    {
                      "title": "Forbidden",
                      "description": "insufficient token scope"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            });

        var cacheRoot = CreateTempDirectory();
        try
        {
            using var client = CreateClient(handler);
            var repository = new BangumiRepository(
                new BangumiApiClient(client),
                new BangumiCacheStore(cacheRoot));

            var error =
                await Assert.ThrowsAsync<BangumiApiException>(
                    () =>
                        repository.SetUserSubjectCollectionTypeAsync(
                            "secret-test-token",
                            123,
                            BangumiCollectionType.Doing,
                            TestContext.Current.CancellationToken));

            Assert.Equal(
                HttpStatusCode.Forbidden,
                error.StatusCode);
            Assert.Equal(
                "Forbidden",
                error.ServerTitle);
            Assert.Equal(
                "insufficient token scope",
                error.ServerDescription);
            Assert.Contains(
                "insufficient token scope",
                error.DisplayDetail,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"description\"",
                error.ResponseBody,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SetSubjectCollectionType_PostsBearerTokenAndType()
    {
        const string token = "secret-test-token";
        HttpMethod? method = null;
        string? requestUri = null;
        string? authScheme = null;
        string? authToken = null;
        string? contentType = null;
        string? requestBody = null;

        var handler = new CallbackHandler(request =>
        {
            method = request.Method;
            requestUri = request.RequestUri!.ToString();
            authScheme = request.Headers.Authorization?.Scheme;
            authToken = request.Headers.Authorization?.Parameter;
            contentType =
                request.Content?.Headers.ContentType?.ToString();
            requestBody = request.Content?.ReadAsStringAsync()
                .GetAwaiter()
                .GetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var cacheRoot = CreateTempDirectory();
        try
        {
            using var client = CreateClient(handler);
            var repository = new BangumiRepository(
                new BangumiApiClient(client),
                new BangumiCacheStore(cacheRoot));

            await repository.SetUserSubjectCollectionTypeAsync(
                token,
                123,
                BangumiCollectionType.OnHold,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpMethod.Post, method);
            Assert.Contains(
                "/v0/users/-/collections/123",
                requestUri,
                StringComparison.Ordinal);
            Assert.Equal("Bearer", authScheme);
            Assert.Equal(token, authToken);
            Assert.Equal("application/json", contentType);

            using var requestJson =
                System.Text.Json.JsonDocument.Parse(
                    requestBody!);
            Assert.Equal(
                (int)BangumiCollectionType.OnHold,
                requestJson.RootElement
                    .GetProperty("type")
                    .GetInt32());
        }
        finally
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

}
