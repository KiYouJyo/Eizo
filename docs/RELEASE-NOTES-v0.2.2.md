简体中文 | [日本語](RELEASE-NOTES-v0.2.2.ja.md) | [English](RELEASE-NOTES-v0.2.2.en.md)

# Eizo v0.2.2 更新器修复与中尺寸导航体验修正

本版本主要用于修复 v0.2.1 之后发现的更新与响应式界面问题，并作为应用内更新链路的回归测试版本。

- 修复 GitHub Release 更新下载完成后，签名验证阶段可能因为 WinVerifyTrust 仍持有 MSIXBundle 文件句柄而被错误报告为 `BundleDownloadFailed` 的问题。
- 调整签名验证顺序：先关闭 WinTrust 状态并释放句柄，再读取 `AppxSignature.p7x`；正式 Release CI 会直接使用 Eizo 自己的 `MsixBundleSignatureVerifier` 验证最终签名包。
- 将下载、签名读取、更新暂存写入等错误分开分类，避免后处理失败继续被误报为“下载失败”。
- 小尺寸播放器控制区改为真正的响应式 Grid 布局；空间不足时中间播放控制组自动下沉，避免字幕、音轨、倍速、音量与播放按钮互相重叠。
- 中/小尺寸展开式导航恢复稳定的浅色/深色 Pane 背景，并以 `ShellNavigation.ActualTheme` 为准同步主题，修复浅色模式展开后再收起变黑的问题。
- 中尺寸“分类”二级菜单恢复 WinUI 原生 Flyout 表现，不再强制覆盖为自定义 Mica 材质。
- 分类页标签图标与左侧导航统一使用同一 `Symbol.Library` 图标源。
- “项目与开源”卡片接入真实 GitHub 仓库、Releases 与隐私说明链接。
- 延续 v0.2.1 的播放器全屏、边栏、音量、生命周期和浅色主题修复。

> 推荐从 v0.2.1 直接使用应用内更新升级到 v0.2.2，以验证本次更新器修复。
