[简体中文](RELEASE-NOTES-v1.0.0.md) | [日本語](RELEASE-NOTES-v1.0.0.ja.md) | English

# Eizo v1.0.0

## First-run setup and Microsoft Store readiness

Eizo 1.0 brings first-launch setup into the product experience and begins the final Microsoft Store release-readiness phase.

## Included

- Added a window-level first-run guide following the UrbanPlanToolbox interaction model, separate from normal page navigation.
- Added six steps: Welcome, Media Sources, TMDB, Bangumi, Playback, and Finish.
- Reused the production local-folder and WebDAV source-management flow inside onboarding.
- Integrated the existing TMDB Read Access Token, Windows PasswordVault storage, and connection verification.
- Reused the browser OAuth / PKCE Bangumi sign-in flow and refreshes account state after returning to Eizo.
- Writes audio, primary subtitle, secondary subtitle, and fullscreen-control timeout choices directly to existing playback settings.
- Added a final configuration summary and an option to start scanning all configured sources.
- Added independent onboarding lifecycle persistence; interrupted setup is not falsely marked complete and is offered again on the next launch.
- Bumped product and MSIX versions to 1.0.0 / 1.0.0.0.
- Updated release-validation naming so 1.x acceptance assets are first-class.
- Disabled the GitHub auto-publication gate during 1.0 Store preparation to avoid publishing a final GitHub Release before Store acceptance is complete.

## Compatibility

- Windows 10 2004 / Windows 11
- x64
