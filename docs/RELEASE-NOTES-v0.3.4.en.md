English | [简体中文](RELEASE-NOTES-v0.3.4.md) | [日本語](RELEASE-NOTES-v0.3.4.ja.md)

# Eizo v0.3.4

## WebDAV Large-Scan Resilience & Persistent Progress

- Fixes large WebDAV media-library scans that could fail during long traversals because of transient SSL/TLS or network errors.
- Reuses HTTP/TLS connections while enumerating directories from the same WebDAV source, reducing repeated connection and handshake overhead across subdirectories.
- Adds bounded backoff retries for transient SSL/network failures, timeouts, HTTP 408, 429 and 5xx responses, and raises the per-directory request timeout to 90 seconds.
- Moves scan state from the Sources page instance to an application-level coordinator, so scans continue across navigation and their current progress is restored when returning to the page.
- Shows processed/discovered directory counts and discovered video counts live on the source card. The known directory total grows dynamically as subdirectories are found.
- Disables editing and removal of a source while it is being scanned to avoid races with credentials, scan scope and task state.
- Preserves the previously successful catalog if a scan fails; source items are replaced only after a full scan completes successfully.
- Validated by the WebDAV runtime probe, source-management contract, Recognition integration, WinUI 3 build, Stage 7 playback integration, and MSIX/LibVLC checks.
