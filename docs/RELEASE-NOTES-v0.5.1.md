# Eizo v0.5.1

## Stage 2 / Slice A：TMDB 核心影视身份

- 在 0.5.0 多源媒体模型基础上开始正式接入 TMDB 的 Series / Episode 身份。
- Metadata Snapshot 新增 EpisodeSeasonNumber 与 ProviderEpisodeId，区分“剧集所属作品 ID”和“这一集自己的 Provider ID”。
- TMDB 解析成功时保存精确 Episode ID，并继续使用对应 Episode Still。
- EizoSeries → EizoSeason → EizoEpisode 内部层级开始写入精确的 Episode ExternalIds。
- 严禁把 Series ID 复制到 Episode ExternalIds；例如 TMDB Series 1396 与 Episode 62085 保持不同作用域。
- 识别报告新增 ProviderEpisodeId 与 MetadataEpisodeSeason，方便真实媒体库验收。
- 新增 TMDB Series/Episode/Still 集成测试。

本切片先完成 TMDB Series/Episode 身份链，不提前实现 Stage 3 的完整 Provider Router。Season 的 TMDB 独立 ID、演职人员与更丰富的影视字段将在后续 0.5.x 切片继续接入。
