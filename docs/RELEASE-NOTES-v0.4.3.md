简体中文 | [日本語](RELEASE-NOTES-v0.4.3.ja.md) | [English](RELEASE-NOTES-v0.4.3.en.md)

# Eizo v0.4.3

## Bangumi 账户与“我的追番”

- 新增无服务器 Bangumi 账户连接：用户在 Bangumi 官方 Access Token 页面生成 Token，并在 Eizo 中粘贴连接。
- Access Token 仅保存在 Windows Credential Locker，不写入 `settings.json`、诊断报告或日志。
- 连接前先通过 `/v0/me` 验证 Token，并读取当前 Bangumi 用户资料。
- Bangumi 主按钮“我的追番”从占位页面切换为真实账户页面，读取当前用户动画收藏中 `在看 (type=3)` 的条目。
- “我的追番”显示 Bangumi 头像、昵称、用户名、动画封面、标题、首播日期、评分、排名、观看集数和用户评分。
- 支持“我的追番”分页继续加载、刷新、断开账户以及从作品卡片进入既有 Bangumi 详情页。
- 设置页新增 Bangumi 账户卡片，可打开 Bangumi 官方 Token 页面、连接账户、查看当前连接身份并安全断开。
- Access Token 失效并收到 401 时会自动清理本地连接状态；普通网络故障不会删除已保存 Token。
- 账户与收藏数据不写入公共 Bangumi 缓存目录，当前版本每次从账户 API 读取，避免把私有收藏持久化为普通缓存文件。
- 新增用户资料 / 收藏 JSON 解析测试、Bearer Header 与“在看”筛选 mock 测试，以及 Credential Locker / My Following 静态契约。

本版本仍为只读账户接入：不会修改 Bangumi 收藏类型、评分或单集观看进度。收藏写入与播放进度同步继续留给后续版本。
