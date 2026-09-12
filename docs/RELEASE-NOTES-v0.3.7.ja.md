日本語 | [简体中文](RELEASE-NOTES-v0.3.7.md) | [English](RELEASE-NOTES-v0.3.7.en.md)

# Eizo v0.3.7

## Metadata 取得の独立化・診断強化・コンポーネント更新

- メディアソースカードの「スキャン」と「Metadata 取得」を独立した操作へ分離しました。スキャンはファイル検出・Recognition・ライブラリ更新のみを行い、Metadata 取得は既に検出済みの媒体だけを対象にし、WebDAV ディレクトリを再走査しません。
- Metadata 取得は単独で繰り返し実行でき、Provider の照合、Runtime 更新、取得成功率を短時間で検証できます。ソースカードには取得進捗、成功、未解決、エラー件数を個別表示します。
- Eizo.Metadata 0.2.3 を統合しました。Provider 検索では元の認識タイトルを保持したまま、ディレクトリ番号、シーズン接頭辞、末尾年、ドット区切り、Unicode 句読点など実ライブラリの命名ノイズを保守的に正規化した検索候補を追加します。
- ライブラリの識別レポートを Recognition + Metadata の統合診断レポートへ拡張し、実際の検索語、候補数、1 位/2 位スコア、差分、しきい値、ResolutionReason、Top Candidates、採点根拠を出力します。
- 未解決の Metadata 項目でも Metadata details を確認できるようになりました。
- Metadata は引き続き独立更新可能な外部コンポーネントとして扱い、bundled fallback、バージョン検証、manifest、SHA-256、互換性検査、pending、再起動時の有効化、失敗時フォールバックを維持します。
- 検証済みの同一バージョン外部 Runtime が bundled Runtime を上書きできるよう起動処理を修正し、ダウングレードは禁止したままにしました。通常の更新確認は同一バージョンを再ダウンロードしません。
- Eizo v0.3.7 のインストール検証では、署名済み MSIXBundle、実インストールと起動、bundled fallback、Metadata 0.2.3 外部 Runtime の pending → restart → active、Recognition/Core/Providers DLL の実ロード、one-click インストール/アンインストールを確認済みです。
