日本語 | [简体中文](RELEASE-NOTES-v0.4.1.md) | [English](RELEASE-NOTES-v0.4.1.en.md)

# Eizo v0.4.1

## ライブラリ分類ページ実装と取得済みメタデータ優先表示

- アニメ、映画、ドラマの各ライブラリページを実際の Catalog データへ接続し、CategoryView のデモ内容を廃止しました。
- 各分類ページは総合ライブラリと同じ Catalog → Subject → Detail → Playback の実データ経路を使用し、MediaCategoryKind のみで絞り込みます。
- 総合ライブラリと各分類ページでは、ポスターと有効な Metadata を取得済みの作品を優先表示します。
- 同じ優先度の中では従来どおりタイトル A–Z 順を維持します。総合ライブラリのカテゴリ順も変更しません。
- 集約作品に複数の resolved Metadata がある場合、Poster を持つ Metadata を作品表示用として優先し、取得済みポスターが空表示になる問題を防ぎます。
- Navigation IA と Library Aggregation UI の契約を更新し、実分類ページ、カテゴリフィルタ、Metadata 優先順、Poster 優先集約を検証対象に追加しました。
- Eizo 0.4.0 の実再生キュー、外部字幕、デュアル字幕、WebDAV 再生、外部 Metadata runtime の仕組みは維持し、回帰検証へ含めます。

本バージョンの受け入れ検証では Release ビルド、Windows x64 MSIXBundle 署名、実インストール / 起動、Recognition / Metadata テスト、ライブラリ契約、プレイヤー / 字幕回帰、one-click パッケージ検証を実施します。
