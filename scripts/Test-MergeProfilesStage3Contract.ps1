param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required merge-profile file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$merge = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataMergePolicy.cs'
$service = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataService.cs'
$snapshot = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataSnapshot.cs'
$catalogView = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'

foreach ($required in @(
    'public enum MediaMetadataMergeProfile',
    'Anime = 1',
    'JapaneseLiveAction = 2',
    'GeneralLiveAction = 3',
    'OrderVisualSources(',
    'OrderDetailSources(',
    'OrderCreditSources(',
    '"tmdb"',
    '"bangumi"')) {
    if (-not $merge.Contains($required, [StringComparison]::Ordinal)) {
        throw "Content-specific merge profile is missing: $required"
    }
}

if ($service.Contains(
        'new Provider.AniListArtworkProvider(',
        [StringComparison]::Ordinal)) {
    throw 'AniList must remain skipped before real-content integration.'
}

foreach ($required in @(
    'AniList is intentionally skipped',
    'MergeProfile = mergeResult.Profile.ToString()')) {
    if (-not $service.Contains($required, [StringComparison]::Ordinal)) {
        throw "Merge-profile service integration is missing: $required"
    }
}

if (-not $snapshot.Contains(
        'public string? MergeProfile { get; init; }',
        [StringComparison]::Ordinal)) {
    throw 'MergeProfile is missing from Metadata snapshot.'
}

if (-not $catalogView.Contains(
        'MergeProfile',
        [StringComparison]::Ordinal)) {
    throw 'MergeProfile is missing from recognition diagnostics.'
}

Write-Host 'Eizo 0.5.6 content-specific merge profiles contract PASS.'
