# Eizo 1.0 Microsoft Store readiness

This document tracks repository-side readiness for the first Microsoft Store submission of Eizo 1.0.

## Current product baseline

- Product version: `1.0.0`
- Package version: `1.0.0.0`
- Target: Windows 10 2004 / Windows 11, x64
- Packaging: MSIX / MSIXBundle
- App model: packaged desktop WinUI 3 with `runFullTrust`
- First-run setup: Welcome → Media Sources → TMDB → Bangumi → Playback → Finish
- GitHub stable publication gate remains disabled while Store readiness is in progress.

## Repository-ready items

- [x] 1.0 product and package version contract
- [x] Multi-size Windows/MSIX app assets
- [x] Store artwork baseline under `release/MicrosoftStore/0.6.1/`
- [x] Public product site
- [x] Public privacy page
- [x] Public support page
- [x] Tri-lingual product UI and release notes
- [x] First-run guide with resumable state
- [x] Local and WebDAV source setup in onboarding
- [x] TMDB configuration and connection verification in onboarding
- [x] Bangumi browser OAuth in onboarding
- [x] Playback preference setup in onboarding
- [x] Settings action to reopen the guide
- [x] Dedicated 1.0 validation and signed sideload acceptance flow

## Partner Center blockers

The Store publisher display name is pinned to `Jo Kiyō` and the StoreUpload pipeline deep-verifies the main x64 package plus every generated scale resource package.

The source manifest intentionally still contains the sideload identity:

- `Identity Name="Eizo"`
- `Publisher="CN=AppPublisher"`

Do not invent Store values. After the product name is reserved in Partner Center, replace the package Identity Name and Publisher with the exact values assigned by Partner Center before producing the Store submission package.

The final Store package must be produced with the Store-upload build mode and with package signing disabled; Microsoft signs accepted Store packages.

## Store listing inputs

Use the public Eizo Pages site as the website, privacy-policy, and support destinations. Before certification, confirm all three production URLs resolve without authentication.

Prepare the final listing in Simplified Chinese, Japanese, and English:

- App name
- Short description
- Full description
- Feature list
- Category
- Age-rating questionnaire
- Search terms
- At least one current screenshot per listing language
- Store logo / artwork
- Certification notes where external login and media-source behavior need explanation

## Final package gate

Before generating the Store upload package:

1. Partner Center product name reserved.
2. Manifest identity synchronized from Partner Center.
3. Repository validation PASS on the final 1.0 commit.
4. Stage 7 playback integration PASS.
5. Signed sideload acceptance package installs and launches successfully.
6. First-run guide verified on a clean install.
7. TMDB configuration verified.
8. Bangumi OAuth callback verified on clean and warm activation.
9. Local-folder and WebDAV onboarding verified without automatic scan before Finish.
10. Finish-step scan verified.
11. Privacy and support pages verified publicly.
12. Final screenshots captured from the accepted 1.0 UI.
13. StoreUpload package generated unsigned for Partner Center.
