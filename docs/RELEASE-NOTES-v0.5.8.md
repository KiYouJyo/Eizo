# Eizo v0.5.8

## Stage 5：扫描、刮削与持久化链收口

- 正式固定媒体来源扫描顺序：Discover → 复用已有 Metadata → Metadata 刮削 → Commit → UI Changed。
- 本地媒体与 WebDAV 都继续进入同一条 ScanSourceAsync Metadata 管线。
- 已解析且 Recognition/Metadata Runtime 未变化的文件直接复用持久化 Metadata，不在每次扫描时重复请求 Provider。
- 新增 Last-known-good 保护：普通扫描遇到 Provider 暂时不可用、429、503、超时等问题时保留现有海报、标题、剧集信息和演职人员，不用失败结果清空媒体库。
- 用户主动“重新刮削”仍允许真正的未解析结果替换旧数据，但网络/传输故障同样保留最后一次成功数据。
- RefreshState 区分 Fresh / Reused / RetainedLastKnownGood / Failed。
- LastRefreshAttemptUtc 与 RefreshErrors 进入 Snapshot 和识别报告，便于排查“当前显示旧数据还是本次刚刮到的数据”。
- Identity Binding 在扫描 Metadata 前读取，成功刮削后再更新持久化绑定。
- catalog schema 升级到 v3，同时兼容读取 v1/v2/v3。
- 扫描取消或刮削尚未完成时不会触发最终 Changed，媒体库继续显示上一次完整提交状态。

至此，真实内容全面接入之前的底层任务完成：媒体模型、TMDB、Router、多源 Merge、内容类型策略、持久身份和扫描持久化链均已具备。

- 修正旧协调器的二段式行为：开启扫描时自动刮削后，Metadata 现在直接在同一次 ScanSourceAsync 中完成，不再“先提交文件列表，再启动第二个刮削任务”。
- 本地来源与 WebDAV 都会在 Recognition + Metadata 全部处理完成后一次性 Commit，新旧媒体库状态不会在扫描中途切换。
