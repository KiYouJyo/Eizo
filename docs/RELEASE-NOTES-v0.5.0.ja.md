# Eizo v0.5.0

## Stage 1：マルチソース・メディアモデル基盤

- プロバイダー非依存の Eizo.Media ドメイン層を追加しました。
- EizoMedia / EizoSeries / EizoSeason / EizoEpisode の階層モデルを導入しました。
- MediaFormat、MediaContentDomain、MediaOrigin を独立した軸として分離しました。
- 1 つのメディアに TMDB、Bangumi、AniList など複数の外部 ID を保持できます。
- Recognition / Metadata は識別根拠と出典として維持し、内部モデルへ投影します。
- catalog schema を v2 に更新し、schema v1 の読み込み移行を維持します。
- ライブラリ分類は内部モデルを優先し、既存ロジックをフォールバックとして残します。

この Stage では TMDB / AniList の新規ネットワーク通信や、0.4.7 で確定した詳細画面レイアウトの大幅変更は行いません。
