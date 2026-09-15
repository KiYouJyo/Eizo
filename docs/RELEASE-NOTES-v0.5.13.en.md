[简体中文](RELEASE-NOTES-v0.5.13.md) | [日本語](RELEASE-NOTES-v0.5.13.ja.md) | English

# Eizo v0.5.13

## TMDB Production Integration

Eizo 0.5.13 turns the existing TMDB provider, season/episode, field-merge and routing foundation into a user-facing production workflow. AniList remains intentionally skipped. Bangumi remains the preferred identity source for Japanese animation, while TMDB provides global live-action metadata, visual assets and supplemental fields.

## Included

- Configure a TMDB Read Access Token in Settings → Metadata and store it securely in Windows PasswordVault.
- Keep `EIZO_TMDB_READ_ACCESS_TOKEN` for development, with the environment value taking precedence over the in-app token.
- Get, save, verify and remove the token from Settings without echoing or logging the secret.
- Reload TMDB immediately for scans, full-library scraping, per-title re-scrape and manual matching without restarting Eizo.
- Show only actually available providers in manual matching; requesting an unavailable TMDB provider no longer falls back to Bangumi.
- Persist provider subject IDs and use exact bound identities for subsequent episodes of a series instead of repeating fuzzy title searches.
- Use the existing TMDB movie, TV and Series → Season → Episode pipeline for season metadata, episode titles, overviews, air dates and episode stills.
- Feed TMDB posters, backdrops, episode stills, cast profiles/roles, crew, genres, runtime, production companies, origin countries, status and external IDs through the existing Merge → Snapshot → UI chain.
- Keep Bangumi identity priority for animation while allowing TMDB visual/field supplementation; prefer TMDB for live-action content.
- Add TMDB attribution, official-site navigation and the required disclaimer to About.
- Add a dedicated 0.5.13 production-integration contract and regression tests.

## Credentials

Eizo does not write the TMDB token to the normal settings JSON or repository. An in-app token is stored only in Windows PasswordVault. The current public-distribution model is bring-your-own Read Access Token; no project secret is embedded in the client.
