using Eizo.Bangumi;

namespace Eizo.Bangumi.Tests;

public sealed class BangumiJsonParserTests
{
    [Fact]
    public void PagedSubjects_MapPosterScoreRankAndCollection()
    {
        const string json = """
        {
          "total": 1,
          "limit": 10,
          "offset": 0,
          "data": [
            {
              "id": 123,
              "type": 2,
              "name": "テストアニメ",
              "name_cn": "测试动画",
              "summary": "summary",
              "date": "2026-10-03",
              "platform": "TV",
              "images": {
                "large": "https://lain.bgm.tv/poster.jpg",
                "common": "",
                "medium": "",
                "small": "",
                "grid": ""
              },
              "eps": 12,
              "total_episodes": 13,
              "rating": {
                "rank": 88,
                "total": 100,
                "count": {},
                "score": 8.2
              },
              "collection": {
                "wish": 10,
                "collect": 20,
                "doing": 30,
                "on_hold": 4,
                "dropped": 2
              },
              "meta_tags": [],
              "tags": []
            }
          ]
        }
        """;

        var page = BangumiJsonParser.ParsePagedSubjectPage(json);
        var item = Assert.Single(page.Items);

        Assert.Equal(1, page.Total);
        Assert.Equal(10, page.Limit);
        Assert.Equal(0, page.Offset);
        Assert.False(page.HasMore);
        Assert.Equal(123, item.Id);
        Assert.Equal("测试动画", item.ChineseTitle);
        Assert.Equal("テストアニメ", item.NativeTitle);
        Assert.Equal("https://lain.bgm.tv/poster.jpg", item.PosterUrl);
        Assert.Equal(8.2, item.Score, 3);
        Assert.Equal(88, item.Rank);
        Assert.Equal(13, item.EpisodeCount);
        Assert.Equal(66, item.CollectionTotal);
    }

    [Fact]
    public void Calendar_FiltersNonAnimeAndKeepsWeekday()
    {
        const string json = """
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
                "images": { "large": "https://lain.bgm.tv/a.jpg" },
                "eps": 12,
                "eps_count": 12,
                "rating": { "score": 7.5 },
                "rank": 400,
                "collection": {
                  "wish": 1,
                  "collect": 2,
                  "doing": 3,
                  "on_hold": 4,
                  "dropped": 5
                }
              },
              {
                "id": 11,
                "type": 6,
                "name": "Drama",
                "name_cn": "电视剧"
              }
            ]
          }
        ]
        """;

        var calendar = BangumiJsonParser.ParseCalendar(json);
        var day = Assert.Single(calendar.Days);
        var item = Assert.Single(day.Items);

        Assert.Equal(1, day.WeekdayId);
        Assert.Equal("星期一", day.ChineseName);
        Assert.Equal(10, item.Id);
        Assert.Equal(15, item.CollectionTotal);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 4)]
    [InlineData(6, 4)]
    [InlineData(7, 7)]
    [InlineData(9, 7)]
    [InlineData(10, 10)]
    [InlineData(12, 10)]
    public void SeasonStartMonth_UsesAnimeQuarter(
        int month,
        int expected)
    {
        Assert.Equal(
            expected,
            BangumiRepository.GetSeasonStartMonth(month));
    }

    [Theory]
    [InlineData(2026, 1, -1, 2025, 10)]
    [InlineData(2026, 1, 1, 2026, 4)]
    [InlineData(2026, 10, 1, 2027, 1)]
    [InlineData(2026, 7, -2, 2026, 1)]
    public void ShiftSeason_CrossesYearBoundaries(
        int year,
        int startMonth,
        int delta,
        int expectedYear,
        int expectedMonth)
    {
        var shifted = BangumiRepository.ShiftSeason(
            year,
            startMonth,
            delta);

        Assert.Equal(expectedYear, shifted.Year);
        Assert.Equal(expectedMonth, shifted.StartMonth);
    }
    [Fact]
    public void UserProfile_MapsIdentityAndAvatar()
    {
        const string json = """
        {
          "id": 42,
          "username": "eizo-user",
          "nickname": "Eizo User",
          "user_group": 10,
          "avatar": {
            "large": "https://lain.bgm.tv/l.jpg",
            "medium": "https://lain.bgm.tv/m.jpg",
            "small": "https://lain.bgm.tv/s.jpg"
          },
          "sign": "hello"
        }
        """;

        var profile =
            BangumiJsonParser.ParseUserProfile(json);

        Assert.Equal(42, profile.Id);
        Assert.Equal("eizo-user", profile.UserName);
        Assert.Equal("Eizo User", profile.NickName);
        Assert.Equal("hello", profile.Sign);
        Assert.Equal(
            "https://lain.bgm.tv/m.jpg",
            profile.AvatarMedium);
    }

    [Fact]
    public void UserCollection_MapsWatchingProgressAndSlimSubject()
    {
        const string json = """
        {
          "total": 2,
          "limit": 50,
          "offset": 0,
          "data": [
            {
              "subject_id": 100,
              "subject_type": 2,
              "rate": 8,
              "type": 3,
              "tags": [],
              "ep_status": 6,
              "vol_status": 0,
              "updated_at": "2026-09-13T09:00:00+08:00",
              "private": false,
              "subject": {
                "id": 100,
                "type": 2,
                "name": "Watching Anime",
                "name_cn": "在看动画",
                "short_summary": "summary",
                "date": "2026-07-01",
                "images": {
                  "large": "https://lain.bgm.tv/a.jpg",
                  "common": "",
                  "medium": "",
                  "small": "",
                  "grid": ""
                },
                "volumes": 0,
                "eps": 12,
                "collection_total": 1234,
                "score": 7.9,
                "rank": 321,
                "tags": []
              }
            }
          ]
        }
        """;

        var page =
            BangumiJsonParser.ParseUserCollectionPage(json);
        var item = Assert.Single(page.Items);

        Assert.Equal(2, page.Total);
        Assert.Equal(BangumiCollectionType.Doing, item.Type);
        Assert.Equal(6, item.EpisodeStatus);
        Assert.Equal(8, item.Rate);
        Assert.False(item.IsPrivate);
        Assert.Equal(100, item.Subject.Id);
        Assert.Equal("在看动画", item.Subject.ChineseTitle);
        Assert.Equal(12, item.Subject.EpisodeCount);
        Assert.Equal(1234, item.Subject.CollectionTotal);
        Assert.Equal(7.9, item.Subject.Score, 3);
    }

}
