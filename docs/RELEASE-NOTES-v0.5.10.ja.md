[简体中文](RELEASE-NOTES-v0.5.10.md) | 日本語 | [English](RELEASE-NOTES-v0.5.10.en.md)

# Eizo v0.5.10

## Real Content UI / Slice B：表示モデル統合

- CatalogSubjectPresentation を追加し、ライブラリカードと詳細画面が別々に Metadata を組み立てないようにします。
- ライブラリカードは Poster、優先タイトル、第2タイトル、年、ジャンル、話数、ソース状態を同じ Presentation から表示します。
- 詳細ヘッダーもタイトル、概要、Poster、Backdrop、年、ジャンル、時間、地域、制作会社、制作状態を同じモデルから使用します。
- Provider などの診断情報は通常 UI から除外します。
- Season 選択時は実際の Season Poster を使用し、放送日と概要を既存 ComboBox の Tooltip に表示します。
- Episode カードの第2行に EpisodeAirDate を追加します。
- Episode Still がない場合でも作品 Poster を全話へ複製しません。
