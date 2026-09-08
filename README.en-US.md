# Eizo

[简体中文](./README.md) · [日本語](./README.ja-JP.md)

**Eizo** is a native Windows media library designed with Japanese anime and drama as first-class content, while also supporting movies, TV series, and other personal media.

> Current phase: repository foundation and localization infrastructure.

## Product direction

- Media recognition and metadata optimized first for Japanese anime and drama
- A unified media-source layer for local storage, WebDAV, and commercial cloud drives
- Automatic metadata, related works, seasonal anime organization, and watch progress
- High-quality playback powered by a mature open-source playback engine
- A native Windows experience built with WinUI 3
- Simplified Chinese, Japanese, and English UI

## Languages

| Language | Locale | Product name |
| --- | --- | --- |
| Simplified Chinese | zh-CN | 映藏 |
| Japanese | ja-JP | 映蔵 |
| English | en-US | Eizo |

All three resource files must expose exactly the same resource-key set. See [Localization](./docs/LOCALIZATION.md).

## Development roadmap

Foundation → Japanese media recognition → anime metadata → Japanese drama metadata → playback → library UX → WebDAV → Streaming Gateway → commercial cloud drives.

## Contributing

Read [CONTRIBUTING.md](./CONTRIBUTING.md) before submitting changes. User-facing strings must not be hard-coded; add them to all three localization resources.

## Security

Follow [SECURITY.md](./SECURITY.md) for vulnerability reports. Never post credentials, tokens, signing material, or Microsoft Store secrets in public issues.

---

Eizo / 映藏 / 映蔵
