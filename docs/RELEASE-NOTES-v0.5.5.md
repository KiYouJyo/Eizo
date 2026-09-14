# Eizo v0.5.5

## Stage 3 / Slice B：字段级多源合并

- Provider Router 选出主 Provider 后，不再把其他数据源完全丢弃；已解析的 fallback Provider 会作为补充数据源进入字段级 Merge Policy。
- 主 Provider 继续掌握作品身份与主标题。Anime 以 Bangumi 为主时，Bangumi 标题/简介不会被 TMDB 直接覆盖；真人影视以 TMDB 为主时同理。
- 缺失字段由补充 Provider 填充：简介、上映日期、集数、类型、片长、制作状态、原始语言、来源国家、制作公司、Cast/Crew 等均按“主源优先、缺失补全”处理。
- 列表型字段（Genres、ProductionCompanies、OriginCountryCodes）支持跨源合并与去重。
- Subject ExternalIds 会合并多个 Provider 的作品 ID，使同一个 EizoMedia 可以同时持有 Bangumi/TMDB/IMDb/TVDB 等身份。
- 新增 SeasonExternalIds 与 EpisodeExternalIds，季和单集可以同时保存不同 Provider 的精确 ID，不再受单一 ProviderSeasonId / ProviderEpisodeId 限制。
- Bangumi 主身份的动画仍可从 TMDB 补入精确 Season/Episode ID 与 Episode Still。
- 新增 FieldSources，记录 CanonicalTitle、Overview、RuntimeMinutes、EpisodeThumbnailUrl 等字段最终来自哪个 Provider。
- 新增 MergeContributors，记录参与当前 Metadata Snapshot 的全部数据源。
- 识别报告同步导出 SeasonExternalIds、EpisodeExternalIds、MergeContributors、FieldSources。
- 新增字段合并与多 Provider scoped ID 单元测试。

下一步将继续收口 Artwork 的字段优先级，以及对 Anime / 日本真人影视 / 欧美真人影视分别建立更细的 Merge Profile。
