param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required TMDB season file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$pin = Get-Content -LiteralPath (Join-Path $repoRoot 'eng/Eizo.Metadata.json') -Raw | ConvertFrom-Json
$snapshot = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataSnapshot.cs'
$service = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataService.cs'
$projection = Read-Text 'src/Eizo.MetadataIntegration/MediaModelProjection.cs'
$media = Read-Text 'src/Eizo.Media/MediaModels.cs'
$subject = Read-Text 'src/Eizo.App/Models/CatalogSubjectModel.cs'
$catalogView = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'

if ([string]$pin.version -ne '0.2.22' -or
    [string]$pin.commit -ne 'bfa613b76568ee058ee77060bc8a26b215408406') {
    throw "Eizo.Metadata 0.2.22 pin mismatch: version=$($pin.version) commit=$($pin.commit)"
}

foreach ($required in @(
    'public string? ProviderSeasonId { get; init; }',
    'public string? SeasonTitle { get; init; }',
    'public string? SeasonOverview { get; init; }',
    'public string? SeasonAirDate { get; init; }',
    'public string? SeasonPosterUrl { get; init; }')) {
    if (-not $snapshot.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata snapshot is missing season field: $required"
    }
}

foreach ($required in @(
    'ProviderSeasonId = episode?.ProviderSeasonId',
    'SeasonTitle = episode?.SeasonTitle',
    'SeasonPosterUrl = episode?.SeasonPosterUrl')) {
    if (-not $service.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata service is not preserving TMDB season content: $required"
    }
}

if (-not $projection.Contains('ProjectSeasonExternalIds(', [StringComparison]::Ordinal) -or
    -not $projection.Contains('ProviderSeasonId.Length: > 0', [StringComparison]::Ordinal)) {
    throw 'Exact season ExternalId projection contract is missing.'
}

foreach ($required in @(
    'SeasonExternalIds:',
    'MediaModelProjection.ProjectSeasonExternalIds(',
    'SeasonTitle:',
    'SeasonPosterUrl:')) {
    if (-not $subject.Contains($required, [StringComparison]::Ordinal)) {
        throw "Catalog season projection is incomplete: $required"
    }
}

foreach ($required in @(
    'public string? Title { get; init; }',
    'public string? Overview { get; init; }',
    'public string? AirDate { get; init; }',
    'public string? PosterUrl { get; init; }')) {
    if (-not $media.Contains($required, [StringComparison]::Ordinal)) {
        throw "EizoSeason content model is incomplete: $required"
    }
}

foreach ($column in @(
    'ProviderSeasonId',
    'SeasonTitle',
    'SeasonAirDate',
    'SeasonPosterUrl')) {
    if (-not $catalogView.Contains($column, [StringComparison]::Ordinal)) {
        throw "Recognition report is missing season diagnostic column: $column"
    }
}

Write-Host 'Eizo 0.5.2 TMDB season Stage 2 Slice B contract PASS.'
