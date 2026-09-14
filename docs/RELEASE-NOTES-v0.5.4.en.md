# Eizo v0.5.4

## Stage 3 / Slice A: Provider Router

- Introduces the formal Provider Router instead of letting Bangumi and TMDB compete only through one global candidate score.
- All enabled primary metadata providers still search in parallel; the router chooses Primary and Fallback providers from match evidence and ContentKind.
- For animation with near-tied Bangumi/TMDB evidence, Bangumi is preferred as the work identity provider.
- For live-action TV and movies with near-tied evidence, TMDB is preferred, covering US TV, US movies and general live-action media.
- A clearly stronger match score overrides provider preference; routing never ignores strong identity evidence.
- If providers disagree on Animation vs LiveAction, the router falls back to evidence rather than forcing a policy preference.
- If the primary provider cannot resolve the title, provider-specific resolvers retry in routed fallback order.
- The aggregate candidate set remains available to supplemental field logic, so Bangumi-primary anime can still use TMDB episode stills and AniList/TMDB artwork.
- Recognition diagnostics now include RoutingPrimaryProvider, RoutingFallbackProviders and RoutingReason.
- Adds router unit coverage for anime, live-action, strong-evidence and content-conflict scenarios.

Stage 3 / Slice B will turn provider routing into the formal field-level Merge Policy.
