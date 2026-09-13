[简体中文](RELEASE-NOTES-v0.4.4.md) | [日本語](RELEASE-NOTES-v0.4.4.ja.md) | English

# Eizo v0.4.4

Bangumi account sign-in now uses browser authorization only; the legacy manual Access Token login entry has been removed.

- Bangumi authorization returns to Eizo through `eizo://bangumi-auth` after the Cloudflare relay exchanges the code.
- The protocol URL carries only a short-lived, single-use ticket. `/claim` returns Access/Refresh Tokens, which are validated through `/v0/me` and then stored in Windows Credential Locker.
- The client generates the login state and verifier; the Worker checks state and atomically consumes tickets in a Durable Object.
- Protocol activation works for cold starts and an already-running app. Account changes refresh My Following and Settings.
- Metadata, Recognition, Playback, and existing public Bangumi browsing keep their current architecture.

Bangumi application credentials remain in Cloudflare environment bindings and are excluded from source and acceptance assets.
