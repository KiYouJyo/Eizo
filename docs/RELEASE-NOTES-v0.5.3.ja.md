# Eizo v0.5.3

## Stage 2 / Slice C：TMDB 映像詳細とキャスト・スタッフ

- 公開済み Eizo.Metadata 0.2.23 を固定使用します。
- TMDB のジャンル、上映/話数時間、制作状態、原語、制作国、制作会社を Eizo Snapshot に取り込みます。
- TMDB person ID、役名/職務、部門、プロフィール画像、順序を保持する汎用 Cast / Crew 構造を追加しました。
- 実写ドラマ・映画では TMDB のキャスト/スタッフを優先し、Anime は Bangumi のキャラクター/声優表示を維持します。Bangumi がない場合は Metadata credits へフォールバックします。
- 詳細画面の大枠は変更せず、既存 MetaText にジャンル、時間、地域、制作会社を統合します。
- 既存の人物カードを再利用し、実写では「キャスト / 主要スタッフ」の意味に切り替えます。
- 認識レポートに Genres、Runtime、ProductionCompanies、Origin、Status、Language、CastCount、CrewCount を追加しました。

次は Stage 3 の Provider Router とフィールド単位のマージ優先順位へ進みます。
