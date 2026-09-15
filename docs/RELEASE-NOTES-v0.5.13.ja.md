[简体中文](RELEASE-NOTES-v0.5.13.md) | 日本語 | [English](RELEASE-NOTES-v0.5.13.en.md)

# Eizo v0.5.13

## TMDB Production Integration：TMDB 本番統合

0.5.13 では、これまで実装済みだった TMDB Provider、Season/Episode、フィールドマージ、ルーティングを実際のユーザーワークフローへ接続します。AniList は引き続き対象外です。日本アニメの identity は Bangumi を優先し、TMDB はグローバルな実写作品・画像・補助メタデータを担当します。

## 変更内容

- 「設定 → メタデータ」から TMDB Read Access Token を設定し、Windows PasswordVault に安全に保存できます。
- 開発環境では `EIZO_TMDB_READ_ACCESS_TOKEN` も利用でき、環境変数をアプリ内 Token より優先します。
- Token の取得、保存、オンライン確認、削除を設定画面から実行できます。Token 自体は表示・ログ出力しません。
- TMDB 設定後は再起動せずにスキャン、全体再取得、作品単位の再取得、手動照合へ反映されます。
- 手動照合には実際に利用可能な Provider のみ表示し、TMDB 未設定時に Bangumi へ誤フォールバックしません。
- 解決済み作品の Provider Subject ID を永続化し、同一シリーズの後続エピソードは正確な ID から取得して曖昧検索を省略します。
- 既存 TMDB 内核の Movie / TV / Series → Season → Episode 情報を正式利用し、Season 情報、各話タイトル、概要、放送日、Episode Still を扱います。
- Poster、Backdrop、Episode Still、出演者画像、役名、主要スタッフ、ジャンル、ランタイム、制作会社、国、ステータス、External ID を既存 Merge → Snapshot → UI に反映します。
- アニメは Bangumi identity を優先し、TMDB は画像と利用可能な補助フィールドを提供します。実写作品は TMDB を優先します。
- About に TMDB の出典表示、公式サイトへの導線、必要な免責文を追加しました。
- 0.5.13 専用契約と回帰テストを追加しました。

## 認証情報

TMDB Token は通常の設定 JSON やリポジトリには保存しません。アプリ内 Token は Windows PasswordVault のみに保存されます。現時点の公開配布ではユーザー自身の Read Access Token を利用し、クライアントにプロジェクト Secret を埋め込みません。
