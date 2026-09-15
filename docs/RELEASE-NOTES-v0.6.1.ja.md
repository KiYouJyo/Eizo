[简体中文](RELEASE-NOTES-v0.6.1.md) | 日本語 | [English](RELEASE-NOTES-v0.6.1.en.md)

# Eizo v0.6.1

## ビジュアル、起動体験、詳細画面の改善

0.6.1 では Windows 向けビジュアル資産を刷新し、起動処理と再生詳細画面を整理しました。

## 変更内容

- EXE、ショートカット、インストーラー向けのマルチサイズ `Eizo.ico` を更新。
- AppList、Square44、Square150、Square71、Square310、Wide、StoreLogo、SplashScreen と各 DPI 資産を置換。
- アプリアイコンを白背景・黒線へ統一し、ライトテーマ用の黒背景・白線版を廃止。
- Mica を使ったアプリ内起動画面と Windows ネイティブの青い `ProgressRing` を追加。
- ライブラリ、再生履歴、キャッシュ保守を起動画面の初回描画後に実行するよう起動経路を変更。
- 外部 Playback / Recognition Runtime の実際の有効化を維持するため、早期 Runtime Bootstrap は従来位置に保持。
- 詳細画面で未視聴エピソードは「未視聴」のみを表示し、0% の空進捗バーを非表示化。
- リポジトリ README、GitHub Pages 製品サイト、サポート、プライバシー、リリース手順を整備。

## リリース検証

- x64 Release build PASS。
- 署名済み MSIXBundle のパッケージ、インストール、起動 PASS。
- LibVLC パッケージ検証 PASS（323 plugins）。
- Bundled Metadata 0.2.23 fallback と実際の Recognition / Core / Providers 呼び出し PASS。
- GitHub ワンクリック版と SHA-256 manifest PASS。

## 対応環境

- Windows 10 2004 / Windows 11
- x64
