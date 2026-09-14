[简体中文](RELEASE-NOTES-v0.5.7.md) | [日本語](RELEASE-NOTES-v0.5.7.ja.md) | English

# Eizo v0.5.7

## Stage 4: persistent cross-provider identity bindings

- Adds media-identity-bindings.json to persist EizoMedia ↔ Bangumi/TMDB identities across scans.
- Successful automatic scraping stores ExternalIds and the primary provider for reuse on later scans.
- Adds manual bindings that can pin an EizoMedia to an exact provider Subject ID and optionally make it the primary identity source.
- Manual provider IDs are protected from automatic overwrite.
- Clearing a manual binding invalidates that work's Metadata so the next scrape resolves against the current binding/router state.
- MetadataService can resolve exact bound Subject/Episode data instead of relying only on title search.
- Routing diagnostics distinguish ManualIdentityBinding and PersistedIdentityBinding.
- Recognition diagnostics expose the binding provider and manual/automatic status.
- Identity state is stored separately from catalog.json so source rescans and WebDAV rediscovery do not erase work identity.

After this stage, provider identity is persistent library state rather than a fresh guess on every scan.
