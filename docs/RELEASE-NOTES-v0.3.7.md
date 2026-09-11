简体中文 | [日本語](RELEASE-NOTES-v0.3.7.ja.md) | [English](RELEASE-NOTES-v0.3.7.en.md)

# Eizo v0.3.7

## Metadata 刮削独立化、诊断增强与组件更新

- 将媒体来源卡片中的“扫描”和“刮削”拆分为两个独立操作。扫描只负责发现文件、执行 Recognition 并更新媒体库；刮削只针对已发现媒体执行 Metadata enrichment，不再重复遍历 WebDAV 目录。
- Metadata 刮削现在可以独立、重复执行，便于快速验证 Provider 匹配、Runtime 更新和刮削命中率；卡片单独显示刮削进度、成功、未解决和错误数量。
- 集成 Eizo.Metadata 0.2.3。Provider 搜索会保留原始识别标题，并加入保守规范化搜索词，用于处理目录序号、季前缀、尾部年份、点号分隔和 Unicode 标点等真实媒体库命名噪声。
- 媒体库识别报告升级为统一 Recognition + Metadata 诊断报告，新增实际搜索词、候选数量、第一/第二候选分数、分差、阈值、ResolutionReason、Top Candidates 和评分证据，可直接定位未刮削原因。
- 未解析 Metadata 条目现在也可以查看 Metadata details，不再只允许已解析条目查看诊断。
- Metadata 继续作为独立可更新外置组件，保留 bundled fallback、版本校验、manifest、SHA-256、兼容性检查、pending、重启激活和失败回退。
- 修正组件启动器，使经验证的同版本外置 Runtime 可以接管 bundled Runtime，同时继续禁止降级；更新检查本身仍不会重复下载同版本。
- Eizo v0.3.7 安装验收已覆盖签名 MSIXBundle、实际安装与启动、bundled fallback、Metadata 0.2.3 外置 Runtime 的 pending → restart → active、实际 Recognition/Core/Providers DLL 加载，以及 one-click 安装/卸载包验证。
