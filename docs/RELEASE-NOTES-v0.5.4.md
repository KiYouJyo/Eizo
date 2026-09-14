# Eizo v0.5.4

## Stage 3 / Slice A：Provider Router

- 正式加入 Provider Router，不再让 Bangumi 与 TMDB 仅凭全局候选分数互相“抢主数据源”。
- 所有启用的主数据 Provider 仍会并行搜索，Router 根据匹配分数与 ContentKind 选择主 Provider，并保留其余 Provider 作为 fallback。
- 动画候选在 Bangumi / TMDB 分数接近且内容类型一致时，优先 Bangumi 作为作品身份来源。
- 真人电视剧与电影在 Bangumi / TMDB 分数接近时优先 TMDB，适配美剧、美国电影以及通用真人影视。
- 当某个 Provider 的匹配证据明显更强时，强证据优先，不会为了固定策略强行改选。
- 当 Bangumi 与 TMDB 对 Animation / LiveAction 判断互相冲突时，不套用偏好，按实际匹配分数选择，避免误分类。
- 主 Provider 解析失败后会按 Router 排出的 fallback 顺序继续尝试。
- 全局候选集合仍被保留给字段增强链，因此 Bangumi 主身份的动画仍可继续使用 TMDB Episode Still 与 AniList/TMDB Artwork。
- 识别报告新增 RoutingPrimaryProvider、RoutingFallbackProviders、RoutingReason，方便真实媒体库诊断。
- 新增 Router 单元测试，覆盖动画、真人影视、强证据和内容冲突四类情况。

下一步 Stage 3 / Slice B 将把“选择哪个主 Provider”继续推进为真正的字段级 Merge Policy。
