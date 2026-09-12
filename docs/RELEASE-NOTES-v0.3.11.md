简体中文 | [日本語](RELEASE-NOTES-v0.3.11.ja.md) | [English](RELEASE-NOTES-v0.3.11.en.md)

# Eizo v0.3.11

## 媒体库聚合、卡片交互与 Metadata 运行时链修正

- 媒体库进一步从“逐文件展示”转向作品级聚合。已识别的剧集按作品汇总，并支持在 Recognition 标题一致且存在多个明确季号时，将不同 Provider Subject ID 的不同季合并到同一系列卡片中；详情页继续保留独立 Season / Episode 结构。
- 动画剧场版与电影加入作品级聚合路径；编号明确的动画电影系列可形成 Movie Family，多个来源或重复文件不再天然拆成多张卡片。
- 重做媒体库卡片的 PointerOver / Pressed 高光模板。高光与可见卡片共用同一 12px 圆角几何，解决默认 GridViewItem 高光圆角与卡片圆角不一致的问题。
- 调整媒体库卡片尺寸与信息区高度，避免标题、副标题、年份和来源信息被底部裁切；相关几何已加入 CI UI contract，防止后续回归。
- 集成 Eizo.Metadata 0.2.20，并修正 Provider → Metadata → Snapshot 的 ContentKind 实际落地问题。根因是持久 Metadata 缓存没有按 Runtime 版本隔离，导致 0.2.20 仍可能读取 0.2.19 时代缺少新字段的 Candidate / Subject JSON。
- Metadata 持久缓存现在按 Runtime 版本命名空间隔离，例如 `MetadataCache/runtime-0.2.20/`。Runtime 升级时旧 schema 缓存不再污染新字段；同一 Runtime 内仍可跨进程复用缓存。
- 关于页版本显示不再使用硬编码常量，而是从实际安装包 / 程序集版本动态生成，修复安装 0.3.11 后仍可能显示旧版本号的问题。
- 保持 Playback 与 Metadata 的独立组件更新架构，包括 bundled fallback、版本和 SHA-256 校验、pending → restart → active、失败回退以及外置 Runtime 实际加载验证。
- Eizo v0.3.11 发布验收覆盖 Release 编译、签名 MSIXBundle、实际安装与启动、Metadata 0.2.20 Recognition/Core/Providers bundled fallback 的真实调用、one-click 安装/卸载包以及 SHA-256 校验。

### 已知后续项

- 媒体类型分类仍有需要继续收口的边界案例，例如部分 Movie / 剧场版与 Anime 分类优先级，以及少数 Provider / Resolver 匹配问题。本版本先完成 UI、作品聚合和 Metadata 缓存链修复，不在发布前扩大刮削规则变更范围。
