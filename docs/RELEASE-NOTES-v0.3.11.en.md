English | [简体中文](RELEASE-NOTES-v0.3.11.md) | [日本語](RELEASE-NOTES-v0.3.11.ja.md)

# Eizo v0.3.11

## Library Aggregation, Card Interaction & Metadata Runtime Pipeline Fixes

- Continues the move from per-file library cards to title-level aggregation. When Recognition produces the same normalized title across multiple explicit seasons, seasons backed by different Provider Subject IDs can now be grouped into one series card while the detail view keeps separate Season / Episode structure.
- Adds movies and theatrical anime to title-level grouping. Numbered anime-film collections can form a Movie Family, and duplicate files or multiple sources no longer have to appear as separate cards.
- Replaces the default GridViewItem PointerOver / Pressed visual with a custom overlay that shares the visible card's 12px corner geometry, fixing the mismatch between hover highlight and card radius.
- Adjusts card dimensions and metadata space so title, subtitle, year, and source lines are no longer clipped at the bottom. The geometry is now covered by a CI UI contract.
- Integrates Eizo.Metadata 0.2.20 and fixes the real Provider → Metadata → Snapshot ContentKind pipeline. The root cause was a persistent Metadata cache that was not scoped by Runtime version, allowing 0.2.20 to deserialize 0.2.19 Candidate / Subject JSON that did not contain the new field.
- Persistent Metadata cache is now namespaced by Runtime version, for example `MetadataCache/runtime-0.2.20/`. Runtime upgrades no longer reuse stale schema payloads, while the same Runtime can still reuse cache across processes.
- Removes hard-coded product-version constants from the About page path. Displayed app version now comes from the installed Package / Assembly version, fixing stale About-page version labels after upgrading to 0.3.11.
- Preserves independently updateable Playback and Metadata components with bundled fallback, version and SHA-256 validation, pending → restart → active activation, rollback, and verification that the selected external Runtime is actually loaded.
- v0.3.11 release acceptance covers Release compilation, signed MSIXBundle, real installation and launch, actual bundled Metadata 0.2.20 Recognition/Core/Providers invocation, one-click install/uninstall, and SHA-256 verification.

### Known follow-up

- Media classification still has boundary cases to refine, including Movie / theatrical-title versus Anime precedence and a small number of Provider / Resolver matching cases. This release intentionally closes the UI, title aggregation, and Metadata cache pipeline first instead of broadening scraper rules immediately before publication.
