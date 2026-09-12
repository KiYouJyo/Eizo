# Eizo v0.4.0

## 播放队列、外挂字幕与双字幕

- 播放队列接入真实媒体库聚合集数，支持队列点击切换真实播放源，并在播放结束后自动续播下一项。
- 无章节媒体的上一章节 / 下一章节按钮会自动切换为上一集 / 下一集。
- 自动发现本地与 WebDAV 同目录外挂字幕，支持 SRT、VTT、ASS、SSA。
- 外挂第一字幕与第二字幕统一改为 Eizo WinUI 原生叠加渲染，不再继承 ASS / SSA 文件中的旧式字体样式。
- 双字幕均支持独立垂直位置与背景不透明度调节；四项显示参数会跨换集、队列切换和应用重启持久保存。
- 字幕 Overlay 改为接近播放器全宽的横向布局，优先单行展开，超出可用宽度时才换行。
- 修正特别篇 / OVA 的聚合与播放源选择：显式 OVA / OAD / ONA / SP / SPECIAL 不再与正片同集号合并；旧 Recognition 快照也会被源文件名兜底纠正。
- 特别篇标题不再被正片 Metadata 污染；缺少可靠特别篇标题时使用稳定的 OVA / Special 编号标签。
- 关于页“可独立更新组件”中移除 Playback / 播放内核行，仅保留 Metadata 运行时更新入口；Playback runtime 本身及 bundled fallback 架构保持不变。
- 继续使用 Eizo.Metadata v0.2.20，并保留外置 Metadata runtime 的版本、SHA-256、兼容性、pending → restart → active 与失败回退链。

本版本通过 Release 编译、Windows x64 MSIXBundle 签名、实际安装启动、播放器/字幕契约、Stage 7 Playback Integration 与 Metadata bundled fallback 验证。
