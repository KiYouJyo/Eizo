using Eizo.Media;
using Eizo.MetadataIntegration;
using Eizo.Recognition;

namespace Eizo.Models;

internal static class DemoCatalogData
{
    private const string ArtworkRoot = "ms-appx:///Assets/Demo";

    public static List<CatalogMediaItemModel> Create()
    {
        var items = new List<CatalogMediaItemModel>();

        AddSeries(
            items,
            id: "after-rain-terminal",
            title: "雨上がりの終着駅",
            localizedTitle: "雨后的终点站",
            contentKind: "Animation",
            year: 2026,
            genres: ["青春", "剧情", "奇幻"],
            overview: "一座只在雨停后出现的终点站，让错过彼此的人获得一次重新选择告别方式的机会。",
            poster: "poster-01.png",
            backdrop: "backdrop-01.png",
            company: "North Window Animation",
            episodes:
            [
                "雨の向こう側",
                "二番線の約束",
                "忘れ物取扱所",
                "終電のあとで",
                "青い傘",
                "また会う日まで",
            ]);

        AddSeries(
            items,
            id: "blue-meridian",
            title: "蒼の子午線",
            localizedTitle: "苍蓝子午线",
            contentKind: "Animation",
            year: 2025,
            genres: ["科幻", "冒险", "海洋"],
            overview: "海平面上升后的近未来，少年测绘员沿着被称作“苍之子午线”的禁航带寻找失落的观测站。",
            poster: "poster-02.png",
            backdrop: "backdrop-02.png",
            company: "Meridian Pictures",
            episodes:
            [
                "0°00′",
                "漂流観測点",
                "青の境界",
                "第七码頭",
                "深海からの信号",
            ]);

        AddSeries(
            items,
            id: "kanda-lanterns",
            title: "神田灯影",
            localizedTitle: "神田灯影",
            contentKind: "Animation",
            year: 2024,
            genres: ["悬疑", "都市", "历史"],
            overview: "旧书店街每逢月末都会亮起一盏无人点燃的灯。大学生摄影师循着灯影，拼起一段消失的城市记忆。",
            poster: "poster-03.png",
            backdrop: "backdrop-03.png",
            company: "Kanda Frame",
            episodes:
            [
                "古書街の灯",
                "写真のないアルバム",
                "橋の下の声",
                "九時十三分",
                "灯影",
            ]);

        AddSeries(
            items,
            id: "summer-orbit",
            title: "夏の軌道",
            localizedTitle: "夏日轨道",
            contentKind: "Animation",
            year: 2026,
            genres: ["青春", "科幻", "校园"],
            overview: "地方高中废部前最后一个暑假，四名天文社成员决定把自制探测器送上平流层。",
            poster: "poster-04.png",
            backdrop: "backdrop-04.png",
            company: "Aster Works",
            episodes:
            [
                "高度0メートル",
                "風を読む",
                "発射前夜",
                "夏の軌道",
                "帰還",
            ]);

        AddMovie(
            items,
            id: "glass-horizon",
            title: "ガラスの地平線",
            localizedTitle: "玻璃地平线",
            year: 2025,
            genres: ["剧情", "科幻"],
            overview: "城市边缘出现一条像玻璃一样反射天空的地平线，一名建筑摄影师开始记录它每日改变的位置。",
            poster: "poster-05.png",
            backdrop: "backdrop-05.png",
            company: "Still Frame Studio",
            runtimeMinutes: 118);

        AddMovie(
            items,
            id: "last-train-minato",
            title: "港行き最終列車",
            localizedTitle: "开往港口的末班车",
            year: 2024,
            genres: ["剧情", "公路"],
            overview: "末班车上的五名陌生人因铁路故障被迫同行，在天亮前抵达一座即将关闭的旧港。",
            poster: "poster-06.png",
            backdrop: "backdrop-06.png",
            company: "Platform 9 Films",
            runtimeMinutes: 104);

        AddMovie(
            items,
            id: "paper-moon-hotel",
            title: "ペーパームーン・ホテル",
            localizedTitle: "纸月亮旅馆",
            year: 2026,
            genres: ["奇幻", "喜剧", "剧情"],
            overview: "只在满月夜营业的山间旅馆，为每位客人保留一间记录他们未曾选择的人生的房间。",
            poster: "poster-07.png",
            backdrop: "backdrop-07.png",
            company: "Moonlit Cinema",
            runtimeMinutes: 126);

        AddSeries(
            items,
            id: "midnight-archive",
            title: "深夜資料室",
            localizedTitle: "深夜资料室",
            contentKind: "LiveAction",
            year: 2025,
            genres: ["职场", "悬疑", "剧情"],
            overview: "市役所地下一层的资料室只在午夜开放。新来的档案员逐渐发现，每份被遗忘的文件都指向同一场旧事故。",
            poster: "poster-08.png",
            backdrop: "backdrop-08.png",
            company: "East Gate Television",
            episodes:
            [
                "地下へ",
                "欠番",
                "赤い索引",
                "保管期限",
                "午前零時",
                "最後の閲覧者",
            ]);

        AddSeries(
            items,
            id: "fifth-floor-window",
            title: "五階の窓",
            localizedTitle: "五楼的窗",
            contentKind: "LiveAction",
            year: 2026,
            genres: ["都市", "群像", "爱情"],
            overview: "同一栋老公寓五楼的住户每天从窗边看见彼此的生活，却从未真正认识。一次停电改变了这一切。",
            poster: "poster-09.png",
            backdrop: "backdrop-09.png",
            company: "Harbor Street Drama",
            episodes:
            [
                "向かいの灯り",
                "停電",
                "ベランダ越し",
                "雨宿り",
                "五階の窓",
            ]);

        return items;
    }

    private static void AddSeries(
        List<CatalogMediaItemModel> items,
        string id,
        string title,
        string localizedTitle,
        string contentKind,
        int year,
        IReadOnlyList<string> genres,
        string overview,
        string poster,
        string backdrop,
        string company,
        IReadOnlyList<string> episodes)
    {
        for (var index = 0; index < episodes.Count; index++)
        {
            var episodeNumber = index + 1;
            var recognition = CreateRecognition(
                logicalPath: $"[EizoDemo] {title} S01E{episodeNumber:00}.mkv",
                mediaKind: "SeriesEpisode",
                title: title,
                year: year,
                season: 1,
                episode: episodeNumber);

            var metadata = CreateMetadata(
                recognition,
                subjectId: id,
                subjectKind: "Series",
                contentKind: contentKind,
                title: localizedTitle,
                originalTitle: title,
                overview: overview,
                releaseDate: $"{year}-04-01",
                episodeCount: episodes.Count,
                poster: poster,
                backdrop: backdrop,
                genres: genres,
                company: company,
                runtimeMinutes: contentKind == "Animation" ? 24 : 46,
                episodeNumber: episodeNumber,
                episodeTitle: episodes[index]);

            items.Add(new CatalogMediaItemModel(
                SourceTitle: $"{title} S01E{episodeNumber:00}",
                ParsedTitle: title,
                NativeTitle: title,
                Category: contentKind == "Animation"
                    ? MediaCategoryKind.Anime
                    : MediaCategoryKind.Series,
                Meta: $"S01E{episodeNumber:00} · {episodes[index]}",
                Location: DemoLocation(id, $"S01E{episodeNumber:00}.mkv", 1_600_000_000L + index * 40_000_000L),
                Recognition: recognition,
                Metadata: metadata,
                Media: MediaModelProjection.Project(recognition, metadata)));
        }
    }

    private static void AddMovie(
        List<CatalogMediaItemModel> items,
        string id,
        string title,
        string localizedTitle,
        int year,
        IReadOnlyList<string> genres,
        string overview,
        string poster,
        string backdrop,
        string company,
        int runtimeMinutes)
    {
        var recognition = CreateRecognition(
            logicalPath: $"[EizoDemo] {title} ({year}) Movie.mkv",
            mediaKind: "Movie",
            title: title,
            year: year,
            season: null,
            episode: null);

        var metadata = CreateMetadata(
            recognition,
            subjectId: id,
            subjectKind: "Movie",
            contentKind: "LiveAction",
            title: localizedTitle,
            originalTitle: title,
            overview: overview,
            releaseDate: $"{year}-09-12",
            episodeCount: null,
            poster: poster,
            backdrop: backdrop,
            genres: genres,
            company: company,
            runtimeMinutes: runtimeMinutes,
            episodeNumber: null,
            episodeTitle: null);

        items.Add(new CatalogMediaItemModel(
            SourceTitle: title,
            ParsedTitle: title,
            NativeTitle: title,
            Category: MediaCategoryKind.Movies,
            Meta: $"{year} · {runtimeMinutes} min",
            Location: DemoLocation(id, $"{id}.mkv", 4_800_000_000L),
            Recognition: recognition,
            Metadata: metadata,
            Media: MediaModelProjection.Project(recognition, metadata)));
    }

    private static MediaRecognitionSnapshot CreateRecognition(
        string logicalPath,
        string mediaKind,
        string title,
        int year,
        int? season,
        decimal? episode) =>
        new(
            LogicalPath: logicalPath,
            Status: MediaRecognitionStatus.Recognized,
            MediaKind: mediaKind,
            SpecialKind: string.Empty,
            EpisodePart: string.Empty,
            IsFinalEpisode: false,
            Title: title,
            EpisodeTitle: null,
            TitleCandidates:
            [
                new RecognitionTitleCandidateSnapshot(
                    title,
                    0.99,
                    "demo",
                    true),
            ],
            SeasonNumber: season,
            CourNumber: null,
            EpisodeNumber: episode,
            EpisodeEndNumber: null,
            SpecialNumber: null,
            Year: year,
            Confidence: 0.99,
            ConfidenceLevel: "High",
            IsAmbiguous: false,
            Evidence: [])
        {
            RuntimeVersion = MediaRecognitionService.RuntimeVersion,
        };

    private static MediaMetadataSnapshot CreateMetadata(
        MediaRecognitionSnapshot recognition,
        string subjectId,
        string subjectKind,
        string contentKind,
        string title,
        string originalTitle,
        string overview,
        string releaseDate,
        int? episodeCount,
        string poster,
        string backdrop,
        IReadOnlyList<string> genres,
        string company,
        int runtimeMinutes,
        decimal? episodeNumber,
        string? episodeTitle) =>
        new(
            RuntimeVersion: MediaMetadataService.RuntimeVersion,
            RecognitionRuntimeVersion: recognition.RuntimeVersion,
            Status: MediaMetadataStatus.Resolved,
            Provider: "demo",
            ProviderSubjectId: subjectId,
            SubjectKind: subjectKind,
            CanonicalTitle: title,
            OriginalTitle: originalTitle,
            LocalizedTitles: new Dictionary<string, string>
            {
                ["zh-CN"] = title,
                ["ja-JP"] = originalTitle,
                ["en-US"] = EnglishTitle(subjectId),
            },
            Aliases: [],
            Overview: overview,
            ReleaseDate: releaseDate,
            EpisodeCount: episodeCount,
            PosterUrl: $"{ArtworkRoot}/{poster}",
            BackdropUrl: $"{ArtworkRoot}/{backdrop}",
            ExternalIds: new Dictionary<string, string>(),
            EpisodeNumber: episodeNumber,
            EpisodeTitle: episodeTitle,
            EpisodeOriginalTitle: episodeTitle,
            EpisodeOverview: episodeTitle is null
                ? null
                : $"《{episodeTitle}》是 Eizo Demo 为宣传截图准备的虚构剧集内容。",
            EpisodeAirDate: episodeNumber is null
                ? null
                : releaseDate,
            EpisodeThumbnailUrl: $"{ArtworkRoot}/{backdrop}",
            Confidence: 1.0,
            Errors: [],
            UpdatedAtUtc: new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero))
        {
            ContentKind = contentKind,
            EpisodeSeasonNumber = episodeNumber is null ? null : 1,
            ProviderEpisodeId = episodeNumber is null
                ? null
                : $"{subjectId}-e{episodeNumber:00}",
            ProviderSeasonId = episodeNumber is null
                ? null
                : $"{subjectId}-s1",
            SeasonTitle = episodeNumber is null ? null : "Season 1",
            SeasonPosterUrl = $"{ArtworkRoot}/{poster}",
            Genres = genres.ToList(),
            ProductionCompanies = [company],
            OriginCountryCodes = ["JP"],
            RuntimeMinutes = runtimeMinutes,
            ProductionStatus = "Released",
            OriginalLanguage = "ja",
            Cast =
            [
                new MediaPersonCreditSnapshot("demo-cast-1", "朝倉 澪", "主演", "Acting", null, 0),
                new MediaPersonCreditSnapshot("demo-cast-2", "水野 朔", "主演", "Acting", null, 1),
                new MediaPersonCreditSnapshot("demo-cast-3", "高瀬 玲", "出演", "Acting", null, 2),
                new MediaPersonCreditSnapshot("demo-cast-4", "白石 凪", "出演", "Acting", null, 3),
            ],
            Crew =
            [
                new MediaPersonCreditSnapshot("demo-crew-1", "森川 景", "导演", "Directing", null, 0),
                new MediaPersonCreditSnapshot("demo-crew-2", "佐伯 文", "编剧", "Writing", null, 1),
            ],
            RoutingPrimaryProvider = "demo",
            RoutingFallbackProviders = [],
            RoutingReason = "Store screenshot demo data",
            FieldSources = new Dictionary<string, string>
            {
                ["title"] = "demo",
                ["overview"] = "demo",
                ["artwork"] = "demo",
            },
            MergeContributors = ["demo"],
            MergeProfile = "store-screenshot-demo",
            SearchTitles = [title, originalTitle],
            CandidateCount = 1,
            BestScore = 1.0,
        };

    private static MediaLocationModel DemoLocation(
        string id,
        string file,
        long sizeBytes) =>
        new(
            SourceId: "eizo-demo-library",
            Kind: MediaLocationKind.RemoteUri,
            Locator: $"https://demo.invalid/{id}/{file}",
            SizeBytes: sizeBytes,
            ModifiedUtc: new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));

    private static string EnglishTitle(string id) =>
        id switch
        {
            "after-rain-terminal" => "After Rain Terminal",
            "blue-meridian" => "Blue Meridian",
            "kanda-lanterns" => "Lanterns of Kanda",
            "summer-orbit" => "Summer Orbit",
            "glass-horizon" => "Glass Horizon",
            "last-train-minato" => "Last Train to Minato",
            "paper-moon-hotel" => "Paper Moon Hotel",
            "midnight-archive" => "Midnight Archive",
            "fifth-floor-window" => "The Fifth-Floor Window",
            _ => id,
        };
}
