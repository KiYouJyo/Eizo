# Recognition integration

Eizo consumes `Eizo.Metadata.Recognition` as a pinned, offline dependency. The app
does not ask metadata providers to infer file identity from raw filenames.

## Dependency pin

`eng/Eizo.Metadata.Recognition.json` pins an exact Eizo.Metadata commit and package
version. `scripts/Restore-EizoMetadata.ps1` restores that commit into a local NuGet
feed, following the same reproducible dependency pattern used for Eizo.Playback.

## Scan flow

```text
Local / WebDAV source
        |
        v
MediaCatalogStore
        |
        +-- logical relative path
        |
        v
Eizo.Metadata.Recognition
        |
        v
CatalogRecognitionModel
        |
        +-- ParsedTitle for current library display
        +-- title candidates for future provider search
        +-- season / cour / episode / special semantics
        +-- confidence / ambiguity
        |
        v
catalog.json schema v2
```

Local scanning passes a path relative to the configured source root. WebDAV scanning
passes `MediaSourceEntry.RelativePath`. Absolute local paths and remote locator URLs
are not recognition inputs.

## Persistence boundary

The app persists its own catalog DTOs rather than serializing Recognition library
types directly. This keeps the catalog schema under Eizo's control and decouples stored
data from future Recognition enum changes.

Recognition evidence is intentionally not persisted. It can contain path-derived
diagnostic values and is not needed for ordinary provider matching.

Schema v2 adds the optional recognition payload. Schema v1 remains readable so existing
catalogs are not discarded; items receive Recognition data the next time their source
is scanned.

## Category boundary

Recognition determines media shape such as episode, movie or special. It does **not**
decide whether a series is anime, Japanese drama, or another library category.
Canonical category and native/localized titles remain metadata-provider responsibilities.

## Failure behavior

Recognition is enrichment, not a playback prerequisite. If recognition fails for an
individual media path, the raw `SourceTitle` and `Location` are kept and the item
remains playable.

## Validation

`scripts/Test-RecognitionIntegrationContract.ps1` protects dependency, privacy,
schema and threading boundaries. The dedicated Stage 7 Recognition workflow also
compiles the WinUI app, verifies the managed Recognition assembly in build output,
builds an unsigned MSIX and verifies the assembly inside the package.
