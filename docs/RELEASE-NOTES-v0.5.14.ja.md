[简体中文](RELEASE-NOTES-v0.5.14.md) | 日本語 | [English](RELEASE-NOTES-v0.5.14.en.md)

# Eizo v0.5.14

## スクレイピング構成の分離と最適化

0.5.14 ではローカルメディアライブラリのオンラインメタデータを TMDB に統一し、スキャン/スクレイピングのスケジューリングを再構成します。Bangumi はローカル Metadata Provider / Router / Merge から外れますが、「視聴管理、放送カレンダー、ランキング・発見、コメント、コミュニティ」は引き続き利用できます。

## 変更内容

- ライブラリ用 MetadataService は Bangumi Provider を作成せず、TMDB を唯一のオンラインメタデータ権威とします。
- 手動照合を TMDB-only に簡素化し、Provider 選択 UI を廃止。
- 旧 Bangumi 主体の保存済みメタデータは 0.5.14 の初回スキャンで TMDB への移行対象になります。
- TMDB 成功後は library identity から旧 Bangumi ID を除去し、IMDb など共通 External ID は保持します。
- 既存の TMDB ID を優先して正確な Subject identity として再利用します。
- 全ファイル逐次処理から作品単位のバッチ処理へ変更。
- 各作品の代表アイテムで TMDB Subject ID を確立し、後続エピソードはその ID を再利用します。
- 同一作品は Season 単位で整理し、同一 Season 内は順番に処理して Season cache を活用します。
- 異なる作品は最大 3 並列、異なる Season は最大 2 並列に制限し、過剰なリクエストや cache stampede を防ぎます。
- ライブラリはバッチ終了後にまとめて Commit し、各話ごとの不要な全体更新を避けます。
- Bangumi 製品機能は独立して維持され、TMDB ライブラリの可用性に依存しません。
