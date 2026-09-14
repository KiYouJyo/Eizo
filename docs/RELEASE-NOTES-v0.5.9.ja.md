[简体中文](RELEASE-NOTES-v0.5.9.md) | 日本語 | [English](RELEASE-NOTES-v0.5.9.en.md)

# Eizo v0.5.9

## Real Content UI / Slice A

- 既存の詳細画面レイアウトを維持したまま「その他」アクションを追加します。
- 再スクレイプは 0.5.8 の Metadata 永続化パイプラインを利用し、完了後に最新 CatalogSubject で詳細タブを再構築します。
- 手動照合 UI を追加し、検索語・Provider・候補作品を選択して 0.5.7 Identity Binding に正確な Subject ID を保存できます。
- 手動照合後は対象ソースを再スクレイプし、詳細画面を最新 Metadata に更新します。
- 手動バインドが存在する場合は解除操作も表示します。
- Provider 名は通常の作品 Meta から除外し、診断情報にのみ残します。
- UI は Bangumi/TMDB 実装を直接呼ばず、共通 Metadata 候補検索 API を利用します。
