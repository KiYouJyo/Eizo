English | [简体中文](RELEASE-NOTES-v0.4.3.md) | [日本語](RELEASE-NOTES-v0.4.3.ja.md)

# Eizo v0.4.3

## Bangumi Account & My Following

- Adds a serverless Bangumi account connection. Users generate a token on Bangumi's official Access Token page and paste it into Eizo.
- The Access Token is stored only in Windows Credential Locker and is never written to `settings.json`, diagnostics, or logs.
- Eizo validates the token through `/v0/me` before saving it and retrieves the current Bangumi user profile.
- The Bangumi parent entry, My Following, is upgraded from a placeholder to a real account page that reads anime collections marked `Watching (type=3)`.
- My Following shows the Bangumi avatar, nickname, username, artwork, title, air date, score, rank, watched-episode count, and the user's rating.
- Supports pagination, refresh, disconnect, and opening existing in-app Bangumi subject details from Following cards.
- Settings gains a Bangumi account card for opening the official token page, connecting, showing the current identity, and securely disconnecting.
- A confirmed 401 invalidates the stored connection; ordinary network failures do not delete the saved token.
- Account and collection data are not persisted into the public Bangumi cache directory. This release reads private account data directly from the account API.
- Adds user/collection JSON parser tests, mock Bearer-header and Watching-filter tests, plus Credential Locker and My Following static contracts.

Account integration remains read-only in this release. Eizo does not modify Bangumi collection state, rating, or episode watch progress yet.
