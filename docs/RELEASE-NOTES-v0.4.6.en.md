# Eizo v0.4.6

## Cache System

Eizo 0.4.6 turns the Cache page into a real video-download manager and unified cache system, with WebDAV playback fully wired into a Range block-cache data path.

### Highlights

- Adds the independent `Eizo.Cache` component for media, Bangumi data, metadata, and subtitle cache.
- Refocuses the Cache page on explicit video downloads; automatically generated data is summarized as App cache with one-click cleanup.
- Adds per-episode cache actions from the series detail page, with tasks appearing immediately in the Cache page.
- Shows live progress, downloaded/total bytes, and smoothed real WebDAV network throughput in MB/s.
- Clicking an active download pauses it; clicking again resumes it. Right-click Delete cancels and cleans partial blocks.
- Completed cards hide the progress area and play directly from local cached blocks without a WebDAV availability probe.
- Uses a single rounded card surface and updates models in place, eliminating card jumping during progress updates.
- Preserves episode labels and can recover episode numbers for legacy completed cache entries from Recognition data.
- Caches WebDAV video in 4 MiB Range blocks and reuses blocks already present locally.
- Supports target-block reads after Seek and forward read-ahead.
- Uses ETag / Last-Modified / Content-Length as remote-media version fingerprints.
- Supports 8 / 16 / 32 / 64 / 128 GB limits, LRU cleanup, and offline-content protection.
- Moves Bangumi JSON and remote external subtitles into the unified cache with legacy migration.
- Keeps direct URI playback as the compatibility fallback for servers without usable Range support.

## Playback 0.2.2

- Adds a host-provided random-access media-source API so Eizo keeps WebDAV credentials and cache policy in the app layer.
- Upstreams the concurrency and lifecycle hardening previously carried by Eizo as a local patch.
- Fixes duplicate DisposeAsync lifecycle handling.
- Eizo now pins the official merged Playback 0.2.2 commit with no local source patch.

## Acceptance

The 0.4.6 acceptance chain covers cache unit tests, download-management contracts, a real non-zero-offset WebDAV Range probe, Playback Stage 7, Release x64 compilation, signed MSIX install/launch, the one-click package, and independently updateable component fallback validation.
