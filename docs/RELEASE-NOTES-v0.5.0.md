# Eizo v0.5.0

## Stage 1：多源媒体模型基础

- 新增独立的 Eizo.Media 领域层，媒体身份不再由单一刮削数据源定义。
- 建立 EizoMedia、EizoSeries、EizoSeason、EizoEpisode 四级模型，并让媒体库聚合作品正式生成内部层级。
- 将媒体形态 MediaFormat、内容领域 MediaContentDomain、来源地区 MediaOrigin 拆分为独立维度。
- 同一媒体可同时保存 TMDB、Bangumi、AniList 及其他 External IDs，但 Provider ID 不再直接充当 Eizo 内部媒体 ID。
- CatalogSubjectModel.Key 已切换为 Eizo 内部媒体 ID；原 metadata|... / recognition|... 聚合键保留为 GroupingKey，仅作为匹配证据和诊断信息。
- Provider ID 只有在同一聚合作品的全部成员中都存在且完全一致时才会上提到作品层，避免把某一季的 Bangumi/TMDB ID 错当成整部系列 ID。
- Recognition / Metadata 继续保存识别与来源证据，并投影到 Eizo 内部媒体模型。
- 媒体库 catalog schema 升级到 v2，并兼容读取 schema v1。
- 媒体库分类开始优先读取内部 MediaFormat / MediaContentDomain，同时保留原有回退逻辑。
- 导出识别报告新增 Eizo Item/Subject ID、GroupingKey、Format、Domain、Origin 与 External IDs，便于在真实媒体库中验证多源模型。
- Bangumi / AniList Provider User-Agent 同步更新为 Eizo 0.5.0。

本阶段不增加新的 TMDB 或 AniList 网络请求，也不改变 0.4.7 已确定的详情页大框架；TMDB 核心影视数据接入留给下一阶段。
