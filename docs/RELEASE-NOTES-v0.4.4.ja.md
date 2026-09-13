[简体中文](RELEASE-NOTES-v0.4.4.md) | 日本語 | [English](RELEASE-NOTES-v0.4.4.en.md)

# Eizo v0.4.4

Bangumi のブラウザー認可を既定のログイン方法にしました。手動のアクセストークン入力は詳細設定の代替手段として残しています。

- Cloudflare relay が認可コードを交換し、`eizo://bangumi-auth` で Eizo に戻ります。
- プロトコル URL には有効期間が短い一回限りの ticket だけを含めます。`/claim` で受け取った Access/Refresh Token は `/v0/me` で検証し、Windows Credential Locker に保存します。
- クライアントが state と検証値を生成し、Worker が state を確認して Durable Object 内で ticket を原子的に消費します。
- アプリの未起動時と起動中のプロトコル呼び出しに対応し、ログイン後に「視聴中」と設定画面を更新します。
- Metadata、Recognition、Playback と既存の Bangumi 公開コンテンツの構成は維持します。

Bangumi アプリの認証情報は Cloudflare の環境バインディングに残し、ソースや受け入れ用ファイルには含めません。
