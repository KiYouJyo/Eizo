# Eizo v0.5.7

## Stage 4：跨数据源身份绑定与持久化

- 新增独立的 media-identity-bindings.json，跨扫描保存 EizoMedia 与 Bangumi/TMDB External IDs 的绑定关系。
- 自动刮削成功后会持久化 ExternalIds 与主 Provider；下一次扫描优先复用已确认的绑定，不再从零猜测作品身份。
- 支持 Manual Binding：可以指定 EizoMedia 对应的 Provider Subject ID，并可指定该 Provider 为主身份来源。
- 手动绑定优先于自动结果；自动更新不会覆盖被手动锁定的 Provider ID。
- 手动解绑后会使对应作品 Metadata 失效，下一次刮削重新按当前绑定/Router 生成。
- MetadataService 支持按已绑定 Provider Subject ID 精确读取作品与剧集，而不是只依赖标题搜索。
- 绑定结果写入 RoutingReason：ManualIdentityBinding / PersistedIdentityBinding。
- Recognition 报告增加 IdentityBindingProvider 与 IdentityBindingManual 诊断字段。
- 绑定层与 catalog.json 分离，媒体文件重新扫描、WebDAV 重建列表时不会丢失作品身份。

这一步完成后，自动匹配不再是每次扫描都重新做的一次性决定。
