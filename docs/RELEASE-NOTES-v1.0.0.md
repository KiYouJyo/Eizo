简体中文 | [日本語](RELEASE-NOTES-v1.0.0.ja.md) | [English](RELEASE-NOTES-v1.0.0.en.md)

# Eizo v1.0.0

## 首次使用向导与 Microsoft Store 发布准备

Eizo 1.0 将首次启动体验纳入正式产品流程，并开始面向 Microsoft Store 的发布收口。

## 本版完成

- 增加与 UrbanPlanToolbox 同体系的窗口级首次使用向导，不侵入普通页面导航。
- 使用 6 步流程：欢迎、媒体来源、TMDB、Bangumi、播放偏好、完成。
- 媒体来源步骤直接复用正式的本地文件夹与 WebDAV 管理能力。
- TMDB 步骤接入现有 Read Access Token、Windows PasswordVault 与在线连接验证。
- Bangumi 步骤复用浏览器 OAuth / PKCE 登录链，授权完成后自动回到 Eizo 并刷新账户状态。
- 播放步骤直接写入现有音轨、主字幕、第二字幕与全屏控制区驻留时间设置。
- 完成页显示配置摘要，并可一次性开始扫描已添加媒体来源。
- 首次使用状态独立持久化；中途退出不会错误标记完成，下次启动会再次提供向导。
- 产品与 MSIX 版本提升为 1.0.0 / 1.0.0.0。
- 发布校验命名规则升级为支持 1.x 版本。
- 1.0 准备阶段关闭 GitHub 自动发布门，避免尚未完成 Store 验收时提前发布正式 GitHub Release。

## 兼容性

- Windows 10 2004 / Windows 11
- x64
