简体中文 | [日本語](RELEASE-NOTES-v0.5.11.ja.md) | [English](RELEASE-NOTES-v0.5.11.en.md)

# Eizo v0.5.11

## Real Content UI / Slice C：单作品刮削与媒体库操作

- “重新刮削”正式改为作品级操作，不再因为一张作品卡而重新刮削整个 WebDAV/本地媒体来源。
- 新增 ScrapeSubjectMetadataAsync：只处理当前 CatalogSubject 包含的媒体文件，并只回写这些条目。
- 新增 ScrapeItemMetadataAsync 底层能力，为后续单集重新刮削保留接口。
- 媒体来源页的 ScrapeSourceMetadataAsync 继续保留，用于用户明确要求刷新整个来源时使用。
- 详情页“重新刮削 / 手动匹配 / 清除手动匹配”全部改走作品级 API。
- 媒体库作品卡右键菜单新增同样三项操作，操作完成后 Catalog 通过既有 Changed 事件只重建当前显示数据。
- 手动匹配窗口抽为共享 MetadataMatchDialog，详情页和媒体库使用同一套 Bangumi/TMDB 候选搜索。
- 手动匹配后只重新刮削这一部作品，不再触发整个来源。
