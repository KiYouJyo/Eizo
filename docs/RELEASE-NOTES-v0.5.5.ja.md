# Eizo v0.5.5

## Stage 3 / Slice B：フィールド単位マルチソースマージ

- Provider Router が Primary を決めた後も、解決済み Fallback Provider を補助データとして Merge Policy に残します。
- Primary Provider は作品アイデンティティと主タイトルを維持します。Anime で Bangumi が Primary の場合、Bangumi のタイトル/概要を TMDB が無条件に上書きしません。
- Primary に不足する概要、公開日、話数、ジャンル、時間、状態、言語、制作国、制作会社、Cast/Crew を補助 Provider から補完します。
- Genres / ProductionCompanies / OriginCountryCodes は複数 Provider から統合・重複排除します。
- Subject ExternalIds は複数 Provider ID を統合します。
- SeasonExternalIds / EpisodeExternalIds を追加し、シーズン・エピソードも複数 Provider の正確な ID を保持できます。
- Bangumi Primary の Anime でも TMDB の Season/Episode ID と Episode Still を保持できます。
- FieldSources で各フィールドの出典を、MergeContributors で参加 Provider を記録します。
- 認識レポートにもマージ情報を追加します。

次は Artwork 優先順位とコンテンツ種別別 Merge Profile をさらに整理します。
