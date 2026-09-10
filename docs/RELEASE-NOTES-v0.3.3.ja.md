日本語 | [简体中文](RELEASE-NOTES-v0.3.3.md) | [English](RELEASE-NOTES-v0.3.3.en.md)

# Eizo v0.3.3

## 認識エンジン統合とメディアライブラリ検証

- 独立したオフライン `Eizo.Metadata.Recognition` エンジンを統合し、検証済み Recognition v0.1.0 に固定しました。
- ローカルフォルダーと WebDAV のスキャンは論理相対パスのみを Recognition に渡します。WebDAV URL、ユーザー名、認証情報は入力や evidence に含めません。
- メディアライブラリで認識済み作品名、シーズン/話数、エピソードタイトル、年、信頼度を確認でき、元のファイル名も保持します。
- ライブラリ項目を右クリックして `Recognition details` を選ぶと、Recognition Snapshot、タイトル候補、evidence を確認できます。
- 曖昧・低信頼度・未認識・認識エラーでもスキャンと再生を妨げず、元タイトルを強制的に上書きしません。
- 再生用 `Locator` は変更せず、Recognition はオフライン解析だけを担当します。
- Re:ゼロ、DEATH NOTE の全 bracket 形式、攻殻機動隊 ARISE の Remux 形式、Bilibili タグ形式を含むクロスプラットフォーム統合テストを追加しました。
