简体中文 | [日本語](RELEASE-NOTES-v0.4.4.ja.md) | [English](RELEASE-NOTES-v0.4.4.en.md)

# Eizo v0.4.4

Bangumi 账户连接默认通过浏览器授权。手动 Access Token 输入保留为高级备用登录。

- Eizo 打开 Cloudflare OAuth relay，Bangumi 授权后通过 `eizo://bangumi-auth` 返回应用。
- 仅短时一次性 ticket 出现在协议 URL 中；Access/Refresh Token 经 `/claim` 返回客户端，经 `/v0/me` 验证后保存在 Windows Credential Locker。
- 登录状态与校验值由客户端生成，Worker 在回调时校验 state，并使用 Durable Object 原子消费 ticket。
- 应用冷启动和已运行时的协议激活均进入同一登录处理路径。
- 登录成功后，“我的追番”和设置页根据账户变更事件刷新。
- Metadata、Recognition、Playback 及已有 Bangumi 内容读取保持原有架构。

Cloudflare relay 所需的 Bangumi 应用凭据由 Cloudflare 环境绑定提供，不进入源码或验收产物。
