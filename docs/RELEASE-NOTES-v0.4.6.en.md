# Eizo v0.4.6

## Cache System

Eizo 0.4.6 replaces the old cache placeholder with a real unified cache system and wires WebDAV playback into the block-cache data path.

### Highlights

- Adds the independent `Eizo.Cache` component for media, artwork, metadata, and subtitle cache.
- Shows real disk usage, category totals, cached items, and session-level WebDAV cache diagnostics.
- Supports 8 / 16 / 32 / 64 / 128 GB limits, LRU automatic cleanup, and offline-item protection.
- Moves Bangumi JSON and remote external subtitles into the unified cache with legacy migration.
- Caches WebDAV video in 4 MiB Range blocks and reuses local blocks instead of downloading them again.
- Reads the target block after Seek and performs one-block read-ahead.
- Uses ETag / Last-Modified / Content-Length as remote-media version fingerprints.
- Makes the Remote pre-cache setting enforce a real per-media working-set limit.
- Reuses verified Range support for later items in the playback queue.
- Keeps direct URI playback as the compatibility fallback for servers without usable Range support.
- Adds cache hit rate, memory hits, disk hits, Range download count, and downloaded bytes to the Cache page.

## Playback 0.2.2

- Adds a host-provided random-access media-source API so Eizo keeps WebDAV credentials and cache policy in the app layer.
- Upstreams the playback concurrency and lifecycle hardening that Eizo previously carried as a local patch.
- Fixes duplicate DisposeAsync lifecycle handling.
- Eizo now pins the official merged Playback 0.2.2 commit with no local source patch.

## Acceptance

The 0.4.6 acceptance chain covers cache unit tests, the cache-system contract, a real non-zero-offset WebDAV Range probe, Playback Stage 7, Release x64 compilation, signed MSIX install/launch, and independently updateable component fallback validation.
