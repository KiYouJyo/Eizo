# Eizo v0.5.0

## Stage 1：マルチソース・メディアモデル基盤

- プロバイダー非依存の Eizo.Media ドメイン層を追加しました。
- EizoMedia / EizoSeries / EizoSeason / EizoEpisode の階層モデルを導入し、ライブラリの集約作品も内部階層へ投影します。
- MediaFormat、MediaContentDomain、MediaOrigin を独立した軸として分離しました。
- 1 つのメディアに TMDB、Bangumi、AniList など複数の External IDs を保持できますが、Provider ID は Eizo の内部 ID として直接使用しません。
- CatalogSubjectModel.Key は Eizo 内部メディア ID に移行し、従来の metadata|... / recognition|... キーは GroupingKey としてマッチング根拠・診断用途に限定します。
- Provider ID は、集約作品の全メンバーに存在し、かつ同一値の場合だけ上位スコープへ昇格します。1 シーズンだけの ID をシリーズ全体の ID と誤認しません。
- Recognition / Metadata は識別根拠と出典として維持し、内部モデルへ投影します。
- catalog schema を v2 に更新し、schema v1 の読み込み移行を維持します。
- ライブラリ分類は内部 MediaFormat / MediaContentDomain を優先し、既存ロジックをフォールバックとして残します。
- 認識レポートに Eizo Item/Subject ID、GroupingKey、Format、Domain、Origin、External IDs を追加しました。
- Bangumi / AniList Provider の User-Agent を Eizo 0.5.0 に更新しました。

この Stage では新しい TMDB / AniList ネットワーク通信や、0.4.7 で確定した詳細画面レイアウトの大幅変更は行いません。TMDB のコア映像データ接続は次の Stage に進めます。
