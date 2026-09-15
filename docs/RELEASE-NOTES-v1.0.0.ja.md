[简体中文](RELEASE-NOTES-v1.0.0.md) | 日本語 | [English](RELEASE-NOTES-v1.0.0.en.md)

# Eizo v1.0.0

## 初回セットアップと Microsoft Store リリース準備

Eizo 1.0 では初回起動体験を正式な製品フローへ統合し、Microsoft Store 公開に向けた仕上げを開始します。

## 変更内容

- UrbanPlanToolbox と同系統のウィンドウレベル初回セットアップを追加し、通常のページナビゲーションから分離。
- 6 ステップ構成：ようこそ、メディアソース、TMDB、Bangumi、再生設定、完了。
- メディアソースでは既存のローカルフォルダー / WebDAV 管理機能をそのまま再利用。
- TMDB では既存の Read Access Token、Windows PasswordVault、接続確認を統合。
- Bangumi ではブラウザー OAuth / PKCE を再利用し、認証後に Eizo へ戻ってアカウント状態を更新。
- 再生設定は音声、メイン字幕、第2字幕、全画面コントロール表示時間へ直接反映。
- 完了画面で設定概要を確認し、追加済みメディアソースのスキャンをまとめて開始可能。
- 初回セットアップの完了状態を独立保存。途中終了時は完了扱いにせず、次回起動時に再表示。
- 製品 / MSIX バージョンを 1.0.0 / 1.0.0.0 へ更新。
- リリース検証の命名規則を 1.x に対応。
- Store 検証完了前の正式 GitHub Release を防ぐため、1.0 準備中は GitHub 自動公開ゲートを無効化。

## 対応環境

- Windows 10 2004 / Windows 11
- x64
