param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Provider Router file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$router = Read-Text 'src/Eizo.MetadataIntegration/MediaProviderRouter.cs'
$service = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataService.cs'
$snapshot = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataSnapshot.cs'
$catalogView = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'

foreach ($required in @(
    'public sealed record MediaProviderRoute(',
    'public static class MediaProviderRouter',
    'AnimeIdentityPrefersBangumi',
    'GeneralMediaPrefersTmdb',
    'ProviderContentConflictUseScore',
    'StrongScoreLead')) {
    if (-not $router.Contains($required, [StringComparison]::Ordinal)) {
        throw "Provider routing policy is missing: $required"
    }
}

foreach ($required in @(
    'var aggregateResolution = await _resolver',
    'MediaProviderRouter.Choose(',
    'EnrichWithRouteAsync(',
    '_providerResolvers',
    'Candidates =',
    'aggregateResolution.Candidates')) {
    if (-not $service.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata service is not using routed provider resolution: $required"
    }
}

foreach ($required in @(
    'public string? RoutingPrimaryProvider { get; init; }',
    'public List<string> RoutingFallbackProviders { get; init; } = [];',
    'public string? RoutingReason { get; init; }')) {
    if (-not $snapshot.Contains($required, [StringComparison]::Ordinal)) {
        throw "Routing diagnostics are missing from snapshot: $required"
    }
}

foreach ($column in @(
    'RoutingPrimaryProvider',
    'RoutingFallbackProviders',
    'RoutingReason')) {
    if (-not $catalogView.Contains($column, [StringComparison]::Ordinal)) {
        throw "Recognition report is missing routing diagnostic column: $column"
    }
}

Write-Host 'Eizo 0.5.4 Provider Router Stage 3 Slice A contract PASS.'
