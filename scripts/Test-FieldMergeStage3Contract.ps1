param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required field-merge file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$merge = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataMergePolicy.cs'
$service = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataService.cs'
$snapshot = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataSnapshot.cs'
$projection = Read-Text 'src/Eizo.MetadataIntegration/MediaModelProjection.cs'
$catalogView = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'

foreach ($required in @(
    'public static class MediaMetadataMergePolicy',
    'MediaMetadataProviderSource',
    'MediaMetadataMergeResult',
    'MergeSubjectExternalIds(',
    'MergeScopedExternalIds(',
    'FieldSources',
    'Contributors')) {
    if (-not $merge.Contains($required, [StringComparison]::Ordinal)) {
        throw "Field-level merge policy is missing: $required"
    }
}

foreach ($required in @(
    'LoadSupplementalResultsAsync(',
    'MediaMetadataMergePolicy.Merge(',
    'SeasonExternalIds =',
    'EpisodeExternalIds =',
    'FieldSources =',
    'MergeContributors =')) {
    if (-not $service.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata service does not consume field-level merge policy: $required"
    }
}

foreach ($required in @(
    'public Dictionary<string, string> SeasonExternalIds { get; init; }',
    'public Dictionary<string, string> EpisodeExternalIds { get; init; }',
    'public Dictionary<string, string> FieldSources { get; init; }',
    'public List<string> MergeContributors { get; init; } = [];')) {
    if (-not $snapshot.Contains($required, [StringComparison]::Ordinal)) {
        throw "Merged metadata snapshot is missing: $required"
    }
}

if (-not $projection.Contains(
        'metadata?.SeasonExternalIds is { Count: > 0 }',
        [StringComparison]::Ordinal) -or
    -not $projection.Contains(
        'metadata?.EpisodeExternalIds is { Count: > 0 }',
        [StringComparison]::Ordinal)) {
    throw 'Media model projection is not consuming merged scoped provider IDs.'
}

foreach ($column in @(
    'SeasonExternalIds',
    'EpisodeExternalIds',
    'MergeContributors',
    'FieldSources')) {
    if (-not $catalogView.Contains($column, [StringComparison]::Ordinal)) {
        throw "Recognition report is missing merge diagnostic column: $column"
    }
}

Write-Host 'Eizo 0.5.5 field-level merge Stage 3 Slice B contract PASS.'
