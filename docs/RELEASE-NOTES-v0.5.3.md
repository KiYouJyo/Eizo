# Eizo v0.5.3

## Stage 2 / Slice C：TMDB 通用影视详情与演职人员

- 固定使用已发布的 Eizo.Metadata 0.2.23。
- TMDB 作品详情现在进入 Eizo Snapshot：类型、片长、制作状态、原始语言、来源国家/地区、制作公司。
- 新增通用 Cast / Crew 结构，保留 TMDB person ID、角色/职务、部门、头像与排序。
- 美剧和电影详情页优先使用 TMDB 演员与主创；Anime 仍优先保留 Bangumi 角色/声优体验，Bangumi 缺失时可回退到 Metadata credits。
- 不改变既有详情页大框架：顶部 MetaText 会自然补充类型、片长、地区和制作公司。
- 演职人员卡片沿用既定 UI，真人影视切换为“演员 / 主创与制作人员”语义。
- 识别报告新增 Genres、Runtime、ProductionCompanies、Origin、Status、Language、CastCount、CrewCount 诊断字段。
- Breaking Bad 集成测试覆盖演员 Bryan Cranston、主创 Vince Gilligan、47 分钟片长、美国来源与制作公司。

下一步将进入 Stage 3：正式 Provider Router 与字段级合并优先级。
