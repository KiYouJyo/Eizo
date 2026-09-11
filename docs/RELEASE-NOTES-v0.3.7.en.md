English | [简体中文](RELEASE-NOTES-v0.3.7.md) | [日本語](RELEASE-NOTES-v0.3.7.ja.md)

# Eizo v0.3.7

## Independent Metadata Scraping, Diagnostics & Component Updates

- Separates “Scan” and “Scrape” on each media-source card. Scan now only discovers files, runs Recognition, and updates the library; Scrape only enriches already-discovered media and does not traverse the WebDAV directory tree again.
- Metadata scraping can now be run independently and repeatedly for fast Provider, Runtime, and hit-rate validation. Source cards show scraping progress plus resolved, unresolved, and error counts separately.
- Integrates Eizo.Metadata 0.2.3. Provider search preserves the original recognized title while adding conservative normalized variants for real-library noise such as directory ordinals, season prefixes, trailing years, dotted separators, and Unicode punctuation.
- Expands the library report into unified Recognition + Metadata diagnostics with effective search titles, candidate count, best/second scores, lead, thresholds, ResolutionReason, Top Candidates, and scoring evidence.
- Metadata details are now available for unresolved entries as well as resolved entries.
- Metadata remains an independently updateable external component with bundled fallback, version validation, manifest and SHA-256 checks, compatibility validation, pending state, restart activation, and failure rollback.
- Updates component activation so a validated same-version external Runtime may take over from the bundled Runtime while downgrades remain blocked. Normal update checks still do not re-download the same version.
- Eizo v0.3.7 installation acceptance validates the signed MSIXBundle, real install and launch, bundled fallback, Metadata 0.2.3 external pending → restart → active activation, actual Recognition/Core/Providers DLL loading, and the one-click installer/uninstaller package.
