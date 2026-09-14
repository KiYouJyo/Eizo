param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required TMDB core file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$snapshot = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataSnapshot.cs'
$service = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataService.cs'
$projection = Read-Text 'src/Eizo.MetadataIntegration/MediaModelProjection.cs'
$subject = Read-Text 'src/Eizo.App/Models/CatalogSubjectModel.cs'
$catalogView = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'

foreach ($required in @(
    'public int? EpisodeSeasonNumber { get; init; }',
    'public string? ProviderEpisodeId { get; init; }')) {
    if (-not $snapshot.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata snapshot is missing TMDB episode identity field: $required"
    }
}

foreach ($required in @(
    'ProviderEpisodeId = episode?.ProviderEpisodeId',
    'EpisodeSeasonNumber =',
    'episode?.SeasonNumber')) {
    if (-not $service.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata service is not preserving exact provider episode identity: $required"
    }
}

if (-not $projection.Contains('ProjectEpisodeExternalIds(', [StringComparison]::Ordinal) -or
    -not $projection.Contains('ProviderEpisodeId.Length: > 0', [StringComparison]::Ordinal)) {
    throw 'Episode ExternalId projection contract is missing.'
}

if (-not $subject.Contains('EpisodeExternalIds:', [StringComparison]::Ordinal) -or
    -not $subject.Contains('MediaModelProjection.ProjectEpisodeExternalIds(', [StringComparison]::Ordinal)) {
    throw 'Catalog hierarchy does not consume exact episode ExternalIds.'
}

foreach ($column in @(
    'ProviderEpisodeId',
    'MetadataEpisodeSeason')) {
    if (-not $catalogView.Contains($column, [StringComparison]::Ordinal)) {
        throw "Recognition report is missing TMDB identity diagnostic column: $column"
    }
}

if ($subject -match 'EpisodeExternalIds:\s*[^\r\n]*ProviderSubjectId') {
    throw 'Series/provider subject ID must never be copied into episode ExternalIds.'
}

Write-Host 'Eizo 0.5.1 TMDB core Stage 2 Slice A contract PASS.'
