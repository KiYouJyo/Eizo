param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required media-model file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$media = Read-Text 'src/Eizo.Media/MediaModels.cs'
$projection = Read-Text 'src/Eizo.MetadataIntegration/MediaModelProjection.cs'
$item = Read-Text 'src/Eizo.App/Models/CatalogMediaItemModel.cs'
$subject = Read-Text 'src/Eizo.App/Models/CatalogSubjectModel.cs'
$store = Read-Text 'src/Eizo.App/Models/MediaCatalogStore.cs'
$catalogView = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'

foreach ($required in @(
    'public sealed record EizoMedia(',
    'public sealed record EizoSeries(',
    'public sealed record EizoSeason(',
    'public sealed record EizoEpisode(',
    'public enum MediaFormat',
    'public enum MediaContentDomain',
    'public sealed record MediaOrigin(',
    'public static string CreateInternal(',
    'public static class EizoMediaHierarchy')) {
    if (-not $media.Contains($required, [StringComparison]::Ordinal)) {
        throw "Eizo.Media contract is missing: $required"
    }
}

if (-not $item.Contains('EizoMedia? Media = null', [StringComparison]::Ordinal)) {
    throw 'Catalog item does not persist the Eizo internal media projection.'
}

if (-not $projection.Contains('EizoMediaIdFactory.CreateInternal(', [StringComparison]::Ordinal) -or
    $projection -match 'EizoMediaIdFactory\.Create\s*\(\s*externalIds') {
    throw 'Metadata projection must keep provider IDs out of Eizo internal identity.'
}

if (-not $subject.Contains('public string GroupingKey { get; init; } = Key;', [StringComparison]::Ordinal) -or
    -not $subject.Contains('subjectMedia.Id,', [StringComparison]::Ordinal) -or
    -not $subject.Contains('GroupingKey = identity.Key,', [StringComparison]::Ordinal) -or
    -not $subject.Contains('EizoMediaHierarchy.BuildSeries', [StringComparison]::Ordinal)) {
    throw 'Catalog subject identity/hierarchy contract is incomplete.'
}

if (-not ($store.Contains('SchemaVersion: 2', [StringComparison]::Ordinal) -or
           $store.Contains('SchemaVersion: 3', [StringComparison]::Ordinal)) -or
    -not ($store.Contains('SchemaVersion: 1 or 2', [StringComparison]::Ordinal) -or
           $store.Contains('SchemaVersion: 1 or 2 or 3', [StringComparison]::Ordinal))) {
    throw 'Catalog schema migration contract is missing.'
}

foreach ($column in @(
    'EizoItemMediaId',
    'EizoSubjectId',
    'EizoSubjectGroupingKey',
    'EizoSubjectFormat',
    'EizoSubjectDomain',
    'EizoSubjectExternalIds')) {
    if (-not $catalogView.Contains($column, [StringComparison]::Ordinal)) {
        throw "Recognition report is missing media-model diagnostic column: $column"
    }
}

Write-Host 'Eizo 0.5.0 media model Stage 1 contract PASS.'
