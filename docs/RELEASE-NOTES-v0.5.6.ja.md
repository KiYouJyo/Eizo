# Eizo v0.5.6

## Stage 3 / Slice C：コンテンツ別マージプロファイル

- Anime、日本実写、一般実写の 3 種類の Merge Profile を正式化します。
- Anime は Bangumi の作品アイデンティティ/テキストを維持し、Poster / Backdrop / Season Poster / Episode Still は TMDB を優先します。
- 日本実写は TMDB を構造・画像・詳細の中心とし、Bangumi は日本語/中国語意味情報とコミュニティ補助に残します。
- 欧米などの一般実写は TMDB を中核 Provider とします。
- 実コンテンツ接続前は AniList を明示的にスキップし、ネットワーク呼び出しを行いません。
- Merge Profile は Snapshot と診断レポートに保存します。

これで Provider Router、フィールドマージ、Artwork 優先順位が一つの方針として閉じます。
