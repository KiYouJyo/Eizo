namespace Eizo.Models;

public sealed class MediaCatalogStore
{
    private readonly object _sync = new();
    private readonly List<CatalogMediaItemModel> _items =
    [
        new("葬送的芙莉莲", "葬送的芙莉莲", "葬送のフリーレン", MediaCategoryKind.Anime, "2023 · NTV · 28 · MADHOUSE"),
        new("胆大党", "胆大党", "ダンダダン", MediaCategoryKind.Anime, "2026 秋"),
        new("间谍过家家", "间谍过家家", "SPY×FAMILY", MediaCategoryKind.Anime, "2026 秋"),
        new("链锯人", "链锯人", "チェンソーマン", MediaCategoryKind.Anime, "12"),
        new("蓝色监狱", "蓝色监狱", "ブルーロック", MediaCategoryKind.Anime, "24"),
        new("药屋少女的呢喃", "药屋少女的呢喃", "薬屋のひとりごと", MediaCategoryKind.Anime, "24"),
        new("孤独摇滚！", "孤独摇滚！", "ぼっち・ざ・ろっく！", MediaCategoryKind.Anime, "12"),
        new("Re:从零开始", "Re:从零开始", "Re:ゼロから始める異世界生活", MediaCategoryKind.Anime, "更新中"),

        new("海中沉睡的钻石", "海中沉睡的钻石", "海に眠るダイヤモンド", MediaCategoryKind.Series, "2024 · TBS · 10"),
        new("非自然死亡", "非自然死亡", "アンナチュラル", MediaCategoryKind.Series, "2018 · TBS"),
        new("VIVANT", "VIVANT", "VIVANT", MediaCategoryKind.Series, "2023 · TBS"),
        new("半泽直树", "半泽直树", "半沢直樹", MediaCategoryKind.Series, "2020 · TBS"),
        new("重启人生", "重启人生", "ブラッシュアップライフ", MediaCategoryKind.Series, "2023"),

        new("Sample Movie", "Sample Movie", "Sample Movie", MediaCategoryKind.Movies, "2026 · 120 min"),
        new("Movie A", "Movie A", "Movie A", MediaCategoryKind.Movies, "2026"),
        new("Movie B", "Movie B", "Movie B", MediaCategoryKind.Movies, "2025"),
        new("Movie C", "Movie C", "Movie C", MediaCategoryKind.Movies, "2024")
    ];

    private MediaCatalogStore()
    {
    }

    public static MediaCatalogStore Default { get; } = new();

    public event EventHandler? Changed;

    public IReadOnlyList<CatalogMediaItemModel> Snapshot()
    {
        lock (_sync)
            return _items.ToArray();
    }

    public void RegisterLocalFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var fullPath = Path.GetFullPath(path);
        var sourceTitle = Path.GetFileNameWithoutExtension(fullPath);

        lock (_sync)
        {
            if (_items.Any(item =>
                    !string.IsNullOrWhiteSpace(item.SourcePath) &&
                    string.Equals(
                        Path.GetFullPath(item.SourcePath),
                        fullPath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _items.Add(
                new CatalogMediaItemModel(
                    sourceTitle,
                    ParsedTitle: null,
                    NativeTitle: null,
                    Category: null,
                    Meta: string.Empty,
                    SourcePath: fullPath));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
