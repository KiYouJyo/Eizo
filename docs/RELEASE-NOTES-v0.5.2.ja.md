# Eizo v0.5.2

## Stage 2 / Slice B：TMDB シーズン識別とコンテンツ

- Eizo.Metadata を公開済み 0.2.22 ランタイムへ固定しました。
- TMDB の Series / Season / Episode ID をそれぞれ独立したスコープとして保持します。
- Metadata Snapshot に ProviderSeasonId、SeasonTitle、SeasonOverview、SeasonAirDate、SeasonPosterUrl を追加しました。
- EizoSeason はシーズン名、概要、放送日、シーズンポスターを保持します。
- Catalog 集約は正確な Season ExternalIds と Episode ExternalIds を別々に EizoSeries 階層へ投影し、Series ID を下位へコピーしません。
- 詳細画面の大枠は変更せず、シーズン選択は Provider のローカライズ済みシーズン名を優先します。
- 認識レポートに Season ID / Title / AirDate / Poster を追加しました。
- Breaking Bad の TMDB Series 1396 → Season 3572 → Episode 62085 を回帰テストに含めます。

完全な Provider Router はまだ導入しません。次の Slice では TMDB のキャスト、スタッフ、制作会社などを接続します。
