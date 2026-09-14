# Eizo v0.5.2

## Stage 2 / Slice B：TMDB 季级身份与内容

- Eizo.Metadata 固定版本升级到已发布的 0.2.22 运行时。
- TMDB 的 Series / Season / Episode 三层 Provider ID 现在分别进入独立作用域。
- Metadata Snapshot 新增 ProviderSeasonId、SeasonTitle、SeasonOverview、SeasonAirDate、SeasonPosterUrl。
- EizoSeason 正式承载季标题、简介、首播日期与季海报，不再只是 Season Number 容器。
- Catalog 聚合会把精确的 Season ExternalIds 与 Episode ExternalIds 分别写入 EizoSeries 层级，Series ID 不会向下复制。
- 详情页沿用现有布局，季选择器优先使用 Provider 返回的本地化季标题。
- 识别报告新增季 ID、季标题、季日期和季海报字段，方便真实媒体库验收。
- Breaking Bad 测试链覆盖 TMDB Series 1396 → Season 3572 → Episode 62085。

本切片仍不引入完整 Provider Router。下一步继续补 TMDB 演职人员、制作公司及影视详情字段。
