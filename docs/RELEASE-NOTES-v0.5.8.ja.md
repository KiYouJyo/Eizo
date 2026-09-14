[简体中文](RELEASE-NOTES-v0.5.8.md) | 日本語 | [English](RELEASE-NOTES-v0.5.8.en.md)

# Eizo v0.5.8

## Stage 5：スキャン・メタデータ永続化パイプライン

- ScanSourceAsync を Discover → Metadata 再利用 → Scrape → Commit → UI Changed の順序に固定します。
- ローカルと WebDAV は同じ Metadata パイプラインを使用します。
- Runtime と Recognition が変わらない解決済み Metadata は再利用し、毎回 Provider を呼びません。
- 429 / 503 / timeout など一時的な Provider 障害では Last-known-good Metadata を保持します。
- 手動再スクレイプでは実際の未解決結果を反映できますが、通信障害では既存データを保護します。
- RefreshState、LastRefreshAttemptUtc、RefreshErrors を Snapshot と診断レポートに追加します。
- catalog schema を v3 に更新し、v1/v2/v3 を読み込みます。

これで実コンテンツを全面接続する前の基盤タスクを完了します。

- 自動スクレイプ時の旧 2 段階処理を廃止し、Metadata を同じ ScanSourceAsync 内で完了させます。ファイル一覧を先に Commit してから 2 回目の Metadata Job を起動することはありません。
- ローカル/WebDAV とも Recognition + Metadata 完了後に 1 回だけ Commit します。
