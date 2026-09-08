# 映藏 Eizo

[日本語](./README.ja-JP.md) · [English](./README.en-US.md)

**映藏（Eizo）** 是一款面向 Windows 的原生影音库，优先服务日本动漫与日剧，同时兼容电影、剧集与其他个人媒体。

> 当前阶段：仓库基础设施与本地化框架建设。

## 产品方向

- 日本动漫、日剧优先的媒体识别与元数据体验
- 本地、WebDAV 与商业网盘统一媒体来源
- 自动刮削、作品关系、季度番与播放进度管理
- 成熟开源播放器内核驱动的高质量播放
- WinUI 3 原生 Windows 体验
- 简体中文、日本語、English 三语界面

## 语言

| 语言 | Locale | 产品名 |
| --- | --- | --- |
| 简体中文 | `zh-CN` | 映藏 |
| 日本語 | `ja-JP` | 映蔵 |
| English | `en-US` | Eizo |

本项目要求三种语言资源键保持完全一致。详见 [本地化规范](./docs/LOCALIZATION.md)。

## 开发阶段

仓库当前处于 Foundation 阶段。功能实现将按“基础设施 → 日本媒体识别 → 动漫元数据 → 日剧元数据 → 播放 → 媒体库 UX → WebDAV → Streaming Gateway → 商业网盘”的顺序推进。

## 目录约定

```text
src/                应用与核心代码
tests/              自动化测试
docs/               架构、规范与路线图
scripts/            开发与 CI 辅助脚本
.github/            GitHub 工作流与协作模板
```

## 贡献

提交代码前请阅读 [CONTRIBUTING.md](./CONTRIBUTING.md)。界面文本不得直接硬编码，应进入三语资源文件。

## 安全

安全问题请参考 [SECURITY.md](./SECURITY.md)，不要在公开 Issue 中提交凭据、Token、签名材料或商店密钥。

---

Eizo / 映藏 / 映蔵
