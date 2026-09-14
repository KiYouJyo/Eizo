# Eizo v0.5.0

## Stage 1：多源媒体模型基础

- 新增独立的 Eizo.Media 领域层，媒体身份不再由单一刮削数据源定义。
- 建立 EizoMedia、EizoSeries、EizoSeason、EizoEpisode 四级模型。
- 将媒体形态、内容领域、来源地区拆分为独立维度。
- 同一媒体可同时保存 TMDB、Bangumi、AniList 及其他外部 ID。
- Recognition / Metadata 继续保存识别与来源证据，并投影到 Eizo 内部媒体模型。
- 媒体库 catalog schema 升级到 v2，并兼容读取 schema v1。
- 媒体库分类开始优先读取内部 MediaFormat / MediaContentDomain，同时保留原有回退逻辑。

本阶段不增加 TMDB 或 AniList 网络请求，也不改变 0.4.7 已确定的详情页大框架。
