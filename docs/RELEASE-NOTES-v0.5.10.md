简体中文 | [日本語](RELEASE-NOTES-v0.5.10.ja.md) | [English](RELEASE-NOTES-v0.5.10.en.md)

# Eizo v0.5.10

## Real Content UI / Slice B：展示模型收口

- 新增统一 CatalogSubjectPresentation，媒体库卡片和详情页不再分别拼接标题、年份、海报、类型、来源等字段。
- 媒体库作品卡正式从 Presentation 读取真实 Poster、首选标题、第二标题、年份、类型、集数、Genres 和来源状态。
- 详情页头部同样使用 Presentation：标题、第二标题、简介、Poster、Backdrop、年份、Genres、片长、地区、制作公司与制作状态统一来自同一份 Metadata。
- 普通 UI 不再重复显示 Provider 等诊断字段。
- Season 下拉继续保持原布局，但选择 Season 后会使用真实 Season Poster，并把首播日期/Season Overview 放到下拉提示中。
- Episode 卡片新增 EpisodeAirDate，与原文标题一起进入第二信息行。
- Episode 图像仍坚持 Episode Still 优先；没有单集图片时不会把作品 Poster 复制到所有剧集。
- 不改变已经收口的页面大框架，只替换内容模型和现有控件中的真实数据。
