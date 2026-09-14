# Eizo v0.5.4

## Stage 3 / Slice A：Provider Router

- Bangumi と TMDB が単純な総合スコアだけで主 Provider を競合する状態から、正式な Provider Router へ移行します。
- 有効な主要 Provider は引き続き並列検索し、Router がスコアと ContentKind に基づいて Primary / Fallback を決定します。
- Animation で Bangumi と TMDB が近いスコアなら、作品アイデンティティは Bangumi を優先します。
- 実写 TV / 映画では TMDB を優先し、米国ドラマ・映画を含む一般映像コンテンツに適した経路を使用します。
- 明確なスコア差がある場合は証拠を優先し、固定ルールで上書きしません。
- Animation / LiveAction 判定が Provider 間で衝突した場合も、固定優先ではなく実スコアを使用します。
- Primary が解決できない場合は Router の Fallback 順で再試行します。
- 全 Provider の候補は保持するため、Bangumi 主体の Anime でも TMDB Episode Still と AniList/TMDB Artwork を引き続き利用できます。
- 認識レポートに RoutingPrimaryProvider / RoutingFallbackProviders / RoutingReason を追加します。

次の Slice B ではフィールド単位の Merge Policy を正式化します。
