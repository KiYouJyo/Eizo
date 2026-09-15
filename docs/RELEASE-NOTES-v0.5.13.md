简体中文 | [日本語](RELEASE-NOTES-v0.5.13.ja.md) | [English](RELEASE-NOTES-v0.5.13.en.md)

# Eizo v0.5.13

## TMDB Production Integration：TMDB 正式接入

0.5.13 将此前已经完成的 TMDB Provider、Season/Episode、字段合并与路由能力正式接入用户侧工作流。AniList 继续跳过；日本动画仍以 Bangumi 身份语义为主，TMDB 重点承担全球真人影视、视觉素材和补充元数据。

## 本版完成

- TMDB Read Access Token 可直接在“设置 → 元数据”中配置，并安全保存在 Windows PasswordVault。
- 开发环境仍可使用 `EIZO_TMDB_READ_ACCESS_TOKEN`，且环境变量优先于应用内 Token。
- 设置页支持获取 Token、保存、在线验证与移除；验证不会回显或记录 Token。
- TMDB 配置后立即进入扫描、全库刮削、单作品重新刮削与手动匹配链路，无需重启应用。
- 手动匹配只显示实际可用的数据源；未配置 TMDB 时不会再错误回退到 Bangumi。
- 已解析作品会持久化 Provider Subject ID；后续同系列剧集优先按精确 ID 获取详情，避免重复模糊搜索。
- 电影、电视剧以及 Series → Season → Episode 层级沿用现有 TMDB 内核，支持季度信息、逐集标题/简介/播出日期与 Episode Still。
- TMDB 海报、Backdrop、Episode Still、演员头像、角色、主创、类型、片长、制作公司、国家/地区、状态和外部 ID 已进入现有 Merge → Snapshot → UI 链路。
- 动画继续优先 Bangumi 作为身份来源；TMDB 可补充视觉素材和可用字段。真人影视优先 TMDB。
- About 页增加 TMDB 来源说明、官方站点入口及规定免责声明。
- 新增 0.5.13 TMDB production integration 契约和回归测试。

## 凭证说明

Eizo 不会把 TMDB Token 写入普通设置 JSON 或仓库。应用内 Token 只保存在 Windows PasswordVault。公开发行版目前采用用户自行配置 Read Access Token 的方式，不在客户端内置项目 Secret。

## 数据源策略

- 日本动画：Bangumi identity 优先；TMDB 视觉与字段补全。
- 日本真人剧 / 电影：TMDB 优先，Bangumi 可作为补充。
- 欧美电影 / 电视剧：TMDB 为主要元数据来源。
- AniList：本阶段不接入。
