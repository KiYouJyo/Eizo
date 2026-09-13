简体中文 | [日本語](RELEASE-NOTES-v0.4.5.ja.md) | [English](RELEASE-NOTES-v0.4.5.en.md)

# Eizo v0.4.5

Bangumi 条目页升级为可直接浏览社区内容的桌面入口，本版保持只读，不改动本地媒体库刮削链，也不向 Bangumi 写入收藏、回复或点赞。

- 新增隔离的 `BangumiCommunityClient` / `BangumiCommunityRepository`，Private API `/p1` 与稳定 Public API `/v0` 分离，降低上游接口变动影响。
- 条目详情新增短评、长评、讨论、关联四个社区页签。
- 短评展示用户、收藏状态、评分、时间和 reaction 数，支持分页，以及按“想看 / 看过 / 在看 / 搁置 / 抛弃”筛选。
- 长评支持在 Eizo 内打开全文，展示作者、标签、浏览/回复数、评论与一层嵌套回复。
- 条目讨论支持在 Eizo 内打开主楼、楼层回复与一层嵌套回复。
- 推荐作品与关联条目可直接在 Eizo 内继续打开 Bangumi 条目详情。
- 社区 BBCode 以安全纯文本方式展示，同时保留“在 Bangumi 中打开”网页 fallback。
- 已登录时复用 0.4.4 OAuth 凭据作为可选 Bearer；未登录时仍可读取公开社区内容。
- 单个 Private API 区块失败不会影响现有 Bangumi 基础资料页。
- 增加中、日、英三语社区界面、解析/客户端单元测试、静态集成契约和真实公网 Community API smoke。

本版暂不提供发短评、发帖、回复、点赞或播放进度写回；这些写操作将在后续版本独立处理。
