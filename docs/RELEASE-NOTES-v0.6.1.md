简体中文 | [日本語](RELEASE-NOTES-v0.6.1.ja.md) | [English](RELEASE-NOTES-v0.6.1.en.md)

# Eizo v0.6.1

## 视觉资产、启动体验与详情页完善

0.6.1 完成新一轮 Windows 视觉资产更新，并收口启动阶段与播放详情页的体验。

## 本版完成

- 更新 EXE、快捷方式与安装器使用的多尺寸 `Eizo.ico`。
- 替换 AppList、Square44、Square150、Square71、Square310、Wide、StoreLogo、SplashScreen 及对应 DPI 资源。
- 统一使用白底黑线应用图标，不再为浅色模式使用黑底白线图标。
- 加入应用内 Mica 启动界面，使用原生 Windows 蓝色 `ProgressRing`。
- 调整启动链：媒体库、播放历史与缓存维护在启动界面首帧出现后再执行，减少启动阶段 UI 线程阻塞。
- 保留早期组件 Runtime Bootstrap，避免破坏外置 Playback / Recognition Runtime 的实际激活链。
- 播放详情页中，未观看剧集只显示“未观看”，不再显示 0% 空进度条；已有播放进度保持原显示逻辑。
- 建立新的仓库主页文档、GitHub Pages 产品主页、支持页、隐私页与发布流程文档。

## 发布与验证

- x64 Release 构建通过。
- 签名 MSIXBundle 打包、安装与启动验证通过。
- LibVLC 打包验证通过（323 plugins）。
- Bundled Metadata 0.2.23 fallback 与真实 Recognition / Core / Providers 调用验证通过。
- GitHub 一键安装包与 SHA-256 清单验证通过。

## 兼容性

- Windows 10 2004 / Windows 11
- x64
