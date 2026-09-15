简体中文 | [日本語](RELEASE-NOTES-v0.5.14.ja.md) | [English](RELEASE-NOTES-v0.5.14.en.md)

# Eizo v0.5.14

## 刮削架构解耦与优化

0.5.14 将本地媒体库的在线元数据职责统一到 TMDB，并重构扫描刮削调度。Bangumi 不再参与本地媒体 Metadata Provider / Router / Merge 链，但“我的追番、放送日历、排行与发现、评论与社区”等 Bangumi 产品功能继续保留。

## 本版完成

- 媒体库生产 MetadataService 不再创建 Bangumi Provider，TMDB 成为唯一在线元数据权威。
- 手动匹配改为 TMDB-only，不再向普通用户暴露 Provider 选择。
- 旧 Bangumi-led 媒体快照在 0.5.14 首次扫描时会自动进入 TMDB 升级流程。
- TMDB 成功刮削后移除旧 Bangumi library identity，同时保留 IMDb 等通用 External ID。
- 旧 Bangumi 手动/自动 binding 不再参与媒体库刮削；已有 TMDB ID 会优先作为精确身份使用。
- 刮削调度从“全库逐文件串行”改为“按作品分组”。
- 每个作品先处理一个代表条目建立持久 TMDB Subject ID，后续集直接复用精确身份。
- 同作品继续按 Season 分组；同 Season 内按集顺序处理，使第一集预热 TMDB Season cache，后续集主要进行本地缓存读取和映射。
- 不同作品最多 3 路并行，不同 Season 最多 2 路并行，避免无限并发和同 Season 缓存击穿。
- 媒体库仍在批次完成后统一 Commit，不因单集完成频繁触发完整库刷新。
- Bangumi 模块保持独立，用于追番、日历、排行、发现和社区，不影响 TMDB 媒体库可用性。

## 架构

```
文件 / WebDAV
    ↓
Recognition
    ↓
Subject batching
    ↓
TMDB identity
    ↓
Series / Movie
    ↓
Season batching
    ↓
Episode mapping
    ↓
Metadata Snapshot
    ↓
Batch commit
```

Bangumi：

```
我的追番 / 放送日历 / 排行与发现 / 评论 / 社区
```

不再进入本地文件 Metadata 链。
