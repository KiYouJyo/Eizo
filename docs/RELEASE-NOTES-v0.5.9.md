简体中文 | [日本語](RELEASE-NOTES-v0.5.9.ja.md) | [English](RELEASE-NOTES-v0.5.9.en.md)

# Eizo v0.5.9

## Real Content UI / Slice A

- 详情页沿用既定布局，新增统一的“更多”入口，不重做视觉框架。
- “重新刮削”直接调用 0.5.8 的 Metadata 持久化链，并在完成后用最新 CatalogSubject 重建当前详情页。
- “手动匹配”正式获得 UI：可编辑关键词、选择全部/Bangumi/TMDB、查看候选作品，并把选中的 Provider Subject ID 写入 0.5.7 Identity Binding。
- 手动匹配后立即重新刮削受影响的媒体来源，并把详情页切换到新的真实 Metadata。
- 已有人工绑定时显示“清除手动匹配”，解除锁定后重新进入自动 Router/匹配流程。
- Provider 名称不再作为普通作品 Meta 文本显示；Provider/ID/FieldSources 等信息继续保留在诊断报告中。
- 新增通用 Metadata 候选搜索 API，UI 不需要直接依赖 Bangumi/TMDB Provider 实现。

本版本开始真实内容 UI 接入，但仍保持既有页面结构。下一 Slice 继续收口媒体库作品卡、详情头部、Season/Episode 展示和演职人员 Presentation。
