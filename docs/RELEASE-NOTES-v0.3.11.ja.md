日本語 | [简体中文](RELEASE-NOTES-v0.3.11.md) | [English](RELEASE-NOTES-v0.3.11.en.md)

# Eizo v0.3.11

## ライブラリ集約、カード操作表示、Metadata Runtime 経路の修正

- メディアライブラリをファイル単位表示から作品単位集約へさらに進めました。Recognition のタイトルが一致し、複数の明確なシーズン番号が存在する場合、Provider Subject ID が異なるシーズンも同一シリーズカードへまとめます。詳細画面では Season / Episode 構造を維持します。
- アニメ映画・劇場版と通常映画を作品単位集約の対象に追加しました。番号付きのアニメ映画シリーズは Movie Family としてまとめることができ、複数ソースや重複ファイルが不要に別カードへ分割されにくくなりました。
- GridViewItem の標準 PointerOver / Pressed 表示を置き換え、表示カードと同じ 12px 角丸を使う独自オーバーレイへ変更しました。ホバー表示とカード本体の角丸不一致を修正しています。
- カード寸法と情報領域を調整し、タイトル、サブタイトル、年、ソース表示の下端切れを修正しました。これらのジオメトリは CI の UI contract でも検証します。
- Eizo.Metadata 0.2.20 を統合し、Provider → Metadata → Snapshot の ContentKind が実ライブラリで空になる問題を修正しました。原因は永続 Metadata キャッシュが Runtime バージョンで分離されず、0.2.20 が 0.2.19 時代の ContentKind を持たない Candidate / Subject JSON を再利用できてしまうことでした。
- 永続 Metadata キャッシュを Runtime ごとの名前空間、例: `MetadataCache/runtime-0.2.20/` に分離しました。Runtime 更新時に旧 schema のキャッシュが新しいフィールドを汚染せず、同一 Runtime 内では引き続きプロセスをまたいで再利用できます。
- About 画面のバージョン表示をハードコードから実際の Package / Assembly バージョン由来へ変更し、0.3.11 をインストールしても旧バージョンが表示される問題を修正しました。
- Playback / Metadata の独立コンポーネント更新構成、bundled fallback、バージョン・SHA-256 検証、pending → restart → active、失敗時フォールバック、外部 Runtime の実ロード検証を維持しています。
- v0.3.11 のリリース検証では Release ビルド、署名済み MSIXBundle、実インストールと起動、Metadata 0.2.20 Recognition/Core/Providers bundled fallback の実呼び出し、one-click インストール/アンインストール、SHA-256 を確認しています。

### 今後の既知項目

- 一部の Movie / 劇場版と Anime の分類優先順位、少数の Provider / Resolver 照合など、メディア種別分類には引き続き調整が必要な境界ケースがあります。本リリースでは UI、作品集約、Metadata キャッシュ経路を先に確定し、公開直前にスクレイピング規則を広範囲へ変更していません。
