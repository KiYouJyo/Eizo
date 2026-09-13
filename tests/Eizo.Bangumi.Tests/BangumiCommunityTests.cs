using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Eizo.Bangumi;

namespace Eizo.Bangumi.Tests;

public sealed class BangumiCommunityTests
{
    [Fact]
    public void Comments_MapsUserRatingCollectionAndReactions()
    {
        const string json = """
        {
          "total": 1,
          "data": [
            {
              "id": 77,
              "user": {
                "id": 42,
                "username": "eizo-user",
                "nickname": "Eizo User",
                "avatar": {
                  "large": "https://lain.bgm.tv/l.jpg",
                  "medium": "https://lain.bgm.tv/m.jpg",
                  "small": "https://lain.bgm.tv/s.jpg"
                },
                "group": 10,
                "sign": "hello",
                "joinedAt": 1700000000
              },
              "type": 3,
              "rate": 8,
              "comment": "很好看",
              "updatedAt": 1789257600,
              "reactions": [
                {
                  "value": 1,
                  "users": [
                    { "id": 1, "username": "a", "nickname": "A" },
                    { "id": 2, "username": "b", "nickname": "B" }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var page =
            BangumiCommunityJsonParser.ParseComments(json);
        var item = Assert.Single(page.Items);

        Assert.Equal(1, page.Total);
        Assert.Equal(77, item.Id);
        Assert.Equal("eizo-user", item.User.UserName);
        Assert.Equal(BangumiCollectionType.Doing, item.Type);
        Assert.Equal(8, item.Rate);
        Assert.Equal("很好看", item.Comment);
        Assert.Equal(2, item.ReactionCount);
        Assert.NotNull(item.UpdatedAt);
    }

    [Fact]
    public void ReviewsTopicsRecommendationsAndRelations_MapCoreFields()
    {
        const string reviews = """
        {
          "total": 1,
          "data": [
            {
              "id": 9,
              "user": {
                "id": 1,
                "username": "sai",
                "nickname": "Sai",
                "avatar": { "large": "", "medium": "", "small": "" },
                "group": 10,
                "sign": "",
                "joinedAt": 1
              },
              "entry": {
                "id": 88,
                "type": 0,
                "uid": 1,
                "title": "一篇长评",
                "icon": "",
                "summary": "摘要",
                "replies": 12,
                "public": true,
                "createdAt": 1700000000,
                "updatedAt": 1700000100
              }
            }
          ]
        }
        """;

        const string topics = """
        {
          "total": 1,
          "data": [
            {
              "id": 99,
              "title": "讨论标题",
              "creatorID": 1,
              "parentID": 8,
              "replyCount": 5,
              "createdAt": 1700000000,
              "updatedAt": 1700000200,
              "state": 0,
              "display": 0,
              "creator": {
                "id": 1,
                "username": "sai",
                "nickname": "Sai",
                "avatar": { "large": "", "medium": "", "small": "" },
                "group": 10,
                "sign": "",
                "joinedAt": 1
              }
            }
          ]
        }
        """;

        const string recs = """
        {
          "total": 1,
          "data": [
            {
              "sim": 0.92,
              "count": 18,
              "subject": {
                "id": 10,
                "name": "Related",
                "nameCN": "相关推荐",
                "type": 2,
                "info": "",
                "metaTags": [],
                "rating": { "rank": 123, "count": [], "score": 8.1, "total": 10 },
                "locked": false,
                "nsfw": false,
                "images": {
                  "large": "https://lain.bgm.tv/r.jpg",
                  "common": "",
                  "medium": "",
                  "small": "",
                  "grid": ""
                }
              }
            }
          ]
        }
        """;

        const string relations = """
        {
          "total": 1,
          "data": [
            {
              "order": 1,
              "relation": {
                "id": 2,
                "en": "Sequel",
                "cn": "续集",
                "jp": "続編",
                "desc": ""
              },
              "subject": {
                "id": 11,
                "name": "Next",
                "nameCN": "下一季",
                "type": 2,
                "info": "",
                "metaTags": [],
                "rating": { "rank": 321, "count": [], "score": 7.9, "total": 10 },
                "locked": false,
                "nsfw": false,
                "images": {
                  "large": "https://lain.bgm.tv/n.jpg",
                  "common": "",
                  "medium": "",
                  "small": "",
                  "grid": ""
                }
              }
            }
          ]
        }
        """;

        var review =
            Assert.Single(
                BangumiCommunityJsonParser.ParseReviews(
                    reviews).Items);
        var topic =
            Assert.Single(
                BangumiCommunityJsonParser.ParseTopics(
                    topics).Items);
        var rec =
            Assert.Single(
                BangumiCommunityJsonParser.ParseRecommendations(
                    recs).Items);
        var relation =
            Assert.Single(
                BangumiCommunityJsonParser.ParseRelations(
                    relations).Items);

        Assert.Equal("一篇长评", review.Title);
        Assert.Equal(12, review.ReplyCount);
        Assert.Equal("讨论标题", topic.Title);
        Assert.Equal(5, topic.ReplyCount);
        Assert.Equal(10, rec.Subject.Id);
        Assert.Equal(0.92, rec.Similarity, 3);
        Assert.Equal(11, relation.Subject.Id);
        Assert.Equal("续集", relation.RelationChinese);
    }

    [Fact]
    public void CommunityDetails_MapBlogThreadAndNestedReplies()
    {
        const string blog = """
        {
          "id": 88,
          "type": 0,
          "uid": 1,
          "user": {
            "id": 1,
            "username": "sai",
            "nickname": "Sai",
            "avatar": { "large": "", "medium": "", "small": "" },
            "group": 10,
            "sign": "",
            "joinedAt": 1
          },
          "title": "长评全文",
          "icon": "",
          "content": "[b]正文[/b]",
          "tags": ["动画", "演出"],
          "views": 123,
          "replies": 2,
          "createdAt": 1700000000,
          "updatedAt": 1700000100,
          "noreply": 0,
          "related": 0,
          "public": true
        }
        """;

        const string comments = """
        [
          {
            "id": 501,
            "mainID": 88,
            "creatorID": 2,
            "relatedID": 0,
            "relatedPhotoID": 0,
            "createdAt": 1700000200,
            "content": "主评论",
            "state": 0,
            "user": {
              "id": 2,
              "username": "reader",
              "nickname": "Reader",
              "avatar": { "large": "", "medium": "", "small": "" },
              "group": 10,
              "sign": "",
              "joinedAt": 1
            },
            "reactions": [
              {
                "value": 1,
                "users": [
                  { "id": 9, "username": "x", "nickname": "X" }
                ]
              }
            ],
            "replies": [
              {
                "id": 502,
                "mainID": 88,
                "creatorID": 3,
                "relatedID": 501,
                "relatedPhotoID": 0,
                "createdAt": 1700000300,
                "content": "嵌套回复",
                "state": 0,
                "user": {
                  "id": 3,
                  "username": "reply",
                  "nickname": "Reply",
                  "avatar": { "large": "", "medium": "", "small": "" },
                  "group": 10,
                  "sign": "",
                  "joinedAt": 1
                },
                "reactions": []
              }
            ]
          }
        ]
        """;

        const string topic = """
        {
          "id": 99,
          "title": "讨论全文",
          "creatorID": 1,
          "parentID": 8,
          "replyCount": 1,
          "createdAt": 1700000000,
          "updatedAt": 1700000400,
          "state": 0,
          "display": 0,
          "creator": {
            "id": 1,
            "username": "sai",
            "nickname": "Sai",
            "avatar": { "large": "", "medium": "", "small": "" },
            "group": 10,
            "sign": "",
            "joinedAt": 1
          },
          "subject": {
            "id": 8,
            "name": "Subject",
            "nameCN": "条目",
            "type": 2,
            "info": "",
            "metaTags": [],
            "rating": { "rank": 1, "count": [], "score": 9.1, "total": 10 },
            "locked": false,
            "nsfw": false,
            "images": {
              "large": "",
              "common": "",
              "medium": "",
              "small": "",
              "grid": ""
            }
          },
          "replies": [
            {
              "id": 600,
              "creatorID": 1,
              "createdAt": 1700000000,
              "content": "[b]主楼[/b]",
              "state": 0,
              "creator": {
                "id": 1,
                "username": "sai",
                "nickname": "Sai",
                "avatar": { "large": "", "medium": "", "small": "" },
                "group": 10,
                "sign": "",
                "joinedAt": 1
              },
              "reactions": [],
              "replies": []
            },
            {
              "id": 601,
              "creatorID": 2,
              "createdAt": 1700000500,
              "content": "第一楼",
              "state": 0,
              "creator": {
                "id": 2,
                "username": "reader",
                "nickname": "Reader",
                "avatar": { "large": "", "medium": "", "small": "" },
                "group": 10,
                "sign": "",
                "joinedAt": 1
              },
              "reactions": [],
              "replies": []
            }
          ]
        }
        """;

        var blogDetail =
            BangumiCommunityJsonParser.ParseBlogEntry(blog);
        var blogComments =
            BangumiCommunityJsonParser.ParseBlogComments(comments);
        var topicDetail =
            BangumiCommunityJsonParser.ParseTopicDetail(topic);

        Assert.Equal("长评全文", blogDetail.Title);
        Assert.Equal("[b]正文[/b]", blogDetail.Content);
        Assert.Equal(123, blogDetail.ViewCount);
        var blogComment = Assert.Single(blogComments);
        Assert.Equal(1, blogComment.ReactionCount);
        Assert.Equal("嵌套回复", Assert.Single(blogComment.Replies).Content);

        Assert.Equal(99, topicDetail.TopicId);
        Assert.Equal(8, topicDetail.Subject.Id);
        Assert.NotNull(topicDetail.RootPost);
        Assert.Equal("[b]主楼[/b]", topicDetail.RootPost!.Content);
        Assert.Equal("第一楼", Assert.Single(topicDetail.Replies).Content);
    }

    [Fact]
    public async Task Client_UsesNextBangumiPrivateApiAndBearerToken()
    {
        HttpRequestMessage? captured = null;
        var handler = new CallbackHandler(request =>
        {
            captured = Clone(request);
            return Json("""{"total":0,"data":[]}""");
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(
                "https://next.bgm.tv/",
                UriKind.Absolute),
        };

        var api = new BangumiCommunityClient(client);
        _ = await api.GetSubjectCommentsAsync(
            8,
            limit: 20,
            offset: 40,
            accessToken: "Bearer test-token",
            cancellationToken:
                TestContext.Current.CancellationToken,
            type: BangumiCollectionType.Doing);

        Assert.NotNull(captured);
        using (captured)
        {
            Assert.Equal(
                "https://next.bgm.tv/p1/subjects/8/comments?limit=20&offset=40&type=3",
                captured!.RequestUri!.ToString());
            Assert.Equal(
                new AuthenticationHeaderValue(
                    "Bearer",
                    "test-token"),
                captured.Headers.Authorization);
        }
    }

    [Fact]
    public async Task Client_WritesCommunityPayloadsWithBearerToken()
    {
        var captured = new List<(HttpMethod Method, string Uri, string? Authorization, string? Body)>();
        var handler = new CallbackHandler(request =>
        {
            var body = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            captured.Add((
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                body));
            return Json("""{"id":123}""");
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(
                "https://next.bgm.tv/",
                UriKind.Absolute),
        };

        var api = new BangumiCommunityClient(client);
        _ = await api.CreateSubjectCommentAsync(
            8,
            "test comment",
            BangumiCollectionType.Doing,
            rate: 8,
            turnstileToken: "turnstile-test",
            accessToken: "Bearer access-test",
            TestContext.Current.CancellationToken);

        _ = await api.LikeSubjectPostAsync(
            99,
            value: 0,
            accessToken: "access-test",
            TestContext.Current.CancellationToken);

        Assert.Equal(2, captured.Count);

        var comment = captured[0];
        Assert.Equal(HttpMethod.Post, comment.Method);
        Assert.Equal(
            "https://next.bgm.tv/p1/subjects/8/comments",
            comment.Uri);
        Assert.Equal("Bearer access-test", comment.Authorization);
        Assert.Contains(
            "\"comment\":\"test comment\"",
            comment.Body,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"turnstileToken\":\"turnstile-test\"",
            comment.Body,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"type\":3",
            comment.Body,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"rate\":8",
            comment.Body,
            StringComparison.Ordinal);

        var reaction = captured[1];
        Assert.Equal(HttpMethod.Put, reaction.Method);
        Assert.Equal(
            "https://next.bgm.tv/p1/subjects/-/posts/99/like",
            reaction.Uri);
        Assert.Equal("Bearer access-test", reaction.Authorization);
        Assert.Contains(
            "\"value\":0",
            reaction.Body,
            StringComparison.Ordinal);
    }

    private static HttpRequestMessage Clone(
        HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(
            request.Method,
            request.RequestUri);
        clone.Headers.Authorization =
            request.Headers.Authorization;
        return clone;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                body,
                Encoding.UTF8,
                "application/json"),
        };

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, HttpResponseMessage> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(callback(request));
    }
}
