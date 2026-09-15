[简体中文](RELEASE-NOTES-v0.5.11.md) | 日本語 | [English](RELEASE-NOTES-v0.5.11.en.md)

# Eizo v0.5.11

## Real Content UI / Slice C：作品単位スクレイプとライブラリ操作

- 「メタデータを再取得」を作品単位に変更し、1 枚の作品カードからソース全体を再スクレイプしないようにします。
- ScrapeSubjectMetadataAsync は現在の CatalogSubject に属するメディアだけを更新します。
- 将来の 1 話単位更新用に ScrapeItemMetadataAsync も追加します。
- Media Sources ページ向けの ScrapeSourceMetadataAsync は引き続き残します。
- 詳細画面とライブラリカードの再スクレイプ/手動照合/解除はすべて作品単位 API を使用します。
- 手動照合ダイアログを共有化し、Bangumi/TMDB 候補検索を同じ UI で利用します。
