简体中文 | [日本語](RELEASE-NOTES-v0.4.1.ja.md) | [English](RELEASE-NOTES-v0.4.1.en.md)

# Eizo v0.4.1

## 媒体库分库实际接入与刮削内容优先展示

- 动漫、电影、电视剧三个媒体库分页面正式接入真实 Catalog 数据，不再显示 CategoryView 演示内容。
- 三个分库与总库共用现有 Catalog → Subject → Detail → Playback 链路，分库仅按对应 MediaCategoryKind 过滤，保持详情页、聚合与播放行为一致。
- 总库与分库优先展示已经成功刮削出封面并包含有效 Metadata 信息的作品。
- 同一优先级内继续沿用现有标题 A–Z 排序；总库仍保持动漫、电视剧、电影、未解析的既有分类顺序。
- 聚合作品含有多条 resolved Metadata 时，优先选择带 Poster 的 Metadata 作为作品级展示信息，避免已有封面却显示空白卡片。
- 更新 Navigation IA 与 Library Aggregation UI 契约，覆盖真实分库接入、分类过滤、Metadata 优先排序和 Poster 优先聚合。
- 保持 Eizo 0.4.0 已完成的真实播放队列、外挂字幕、双字幕、WebDAV 播放与 Metadata 外置运行时机制不变，并纳入回归验收。

本版本验收要求包括 Release 编译、Windows x64 MSIXBundle 签名、实际安装与启动、Recognition / Metadata 测试、媒体库契约、播放器 / 字幕回归以及 one-click 安装包校验。
