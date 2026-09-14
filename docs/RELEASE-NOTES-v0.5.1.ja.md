# Eizo v0.5.1

## Stage 2 / Slice A：TMDB コア映像アイデンティティ

- 0.5.0 のマルチソースモデルを基盤に、TMDB の Series / Episode ID を正式に接続します。
- Metadata Snapshot に EpisodeSeasonNumber と ProviderEpisodeId を追加し、作品 ID とエピソード ID を分離します。
- TMDB 解決時に正確な Episode ID を保存し、対応する Episode Still を維持します。
- EizoSeries → EizoSeason → EizoEpisode 階層へ正確な Episode ExternalIds を投影します。
- Series ID を Episode ID としてコピーしません。たとえば TMDB Series 1396 と Episode 62085 は別スコープです。
- 認識レポートに ProviderEpisodeId と MetadataEpisodeSeason を追加しました。
- TMDB Series/Episode/Still の統合テストを追加しました。

この Slice では Stage 3 の完全な Provider Router はまだ導入しません。TMDB Season ID、キャスト/スタッフ、追加の映像フィールドは後続の 0.5.x Slice で進めます。
