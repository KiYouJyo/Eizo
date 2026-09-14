# Eizo v0.5.6

## Stage 3 / Slice C：内容类型合并策略

- 正式区分 Anime、日本真人影视、通用真人影视三种 Merge Profile。
- Anime：Bangumi 继续负责作品身份与主文本，TMDB 优先负责 Poster、Backdrop、Season Poster 与 Episode Still。
- 日本真人影视：TMDB 负责影视结构、视觉资源与详情字段，Bangumi 继续作为日文/中文语义与社区补充来源。
- 欧美及其它真人影视：TMDB 作为核心影视数据源。
- 强制移除当前主线中的 AniList 网络请求；在真实内容接入前明确跳过 AniList，不让第三数据源扩大变量范围。
- Anime 的角色/声优体验仍由现有 Bangumi 模块承担；TMDB credits 只作为 Metadata 层补充。
- Merge Profile 会写入 Snapshot 与识别报告，便于真实媒体库验收时确认每个作品走了哪套策略。
- Artwork 字段开始使用内容类型优先级，而不是单纯“谁先有图就用谁”。

完成这一阶段后，多源模型、Router、字段合并与 Artwork 策略形成闭环。
