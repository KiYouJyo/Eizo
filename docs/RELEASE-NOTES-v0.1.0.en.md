[简体中文](RELEASE-NOTES-v0.1.0.md) | [日本語](RELEASE-NOTES-v0.1.0.ja.md) | English

# Eizo v0.1.0 WinUI 3 UI Foundation

- Delivers the first installable WinUI 3 foundation for Eizo, establishing the title bar, NavigationView shell, Mica backdrop, native card hierarchy and responsive page framework.
- Introduces the same multi-tab model used by SpatialViewer: “+” creates a new home-style workspace tab, shell navigation changes only the current workspace tab, and a separate content tab is created only when a specific title is opened.
- Implements the v0.1 core surfaces for Home, Anime, Movies, Japanese Drama, Media Sources, Cache, About and Settings, with consistent spacing for titles, search, hero cards and native horizontal selectors.
- Adds Simplified Chinese, Japanese and English resources plus Japanese-media-first title preferences, with CI continuously verifying identical resource-key sets across all three locales.
- Reuses SpatialViewer’s theme model for live System, Light and Dark switching, keeping the title bar, navigation pane, tabs, Mica surfaces and ComboBox popups synchronized.
- Ships a signed x64 MSIXBundle, a lightweight one-click bootstrap package and SHA-256 checksums. The bootstrap contains only the public certificate and verification/download scripts—never private keys or the Windows App Runtime.
- v0.1.0 focuses on the UI Foundation and application shell; production metadata scraping, remote media sources, playback engine integration and the complete media-library data path will follow in later releases.
