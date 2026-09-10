# Eizo v0.3.3

## 识别内核接入与媒体库验收

- 接入独立的 `Eizo.Metadata.Recognition` 离线识别内核，并固定到已验证的 Recognition v0.1.0 版本。
- 本地文件夹与 WebDAV 扫描统一使用逻辑相对路径进行识别；WebDAV 地址、用户名和凭据不会进入 Recognition 输入或诊断 evidence。
- 媒体库条目可直接显示识别后的作品名、季/集、单集标题、年份与置信度；原始文件名保留为次级信息。
- 在媒体库条目上右键选择 `Recognition details`，可查看完整 Recognition Snapshot、候选标题与 evidence，便于用真实媒体库验收识别效果。
- 歧义、低置信度、未识别或识别异常不会阻断扫描与播放，也不会强行覆盖原始标题。
- 播放 `Locator` 保持原样，Recognition 仅负责离线解析，不参与 WebDAV 连接和播放地址生成。
- 新增跨平台 Recognition 集成测试，覆盖 Re:0、死亡笔记全 bracket 命名、攻壳 ARISE Remux 命名及 Bilibili 标签命名等真实样本。
