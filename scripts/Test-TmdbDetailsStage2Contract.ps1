param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required TMDB details file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$pin = Get-Content -LiteralPath (Join-Path $repoRoot 'eng/Eizo.Metadata.json') -Raw | ConvertFrom-Json
$snapshot = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataSnapshot.cs'
$service = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataService.cs'
$detail = Read-Text 'src/Eizo.App/Views/DetailView.xaml.cs'
$catalogView = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'

$pinVersion = [Version][string]$pin.version
if ($pinVersion -lt [Version]'0.2.23') {
    throw "Eizo.Metadata 0.2.23+ is required for TMDB general details: version=$($pin.version) commit=$($pin.commit)"
}

foreach ($required in @(
    'public List<string> Genres { get; init; } = [];',
    'public List<string> ProductionCompanies { get; init; } = [];',
    'public List<string> OriginCountryCodes { get; init; } = [];',
    'public int? RuntimeMinutes { get; init; }',
    'public string? ProductionStatus { get; init; }',
    'public string? OriginalLanguage { get; init; }',
    'public List<MediaPersonCreditSnapshot> Cast { get; init; } = [];',
    'public List<MediaPersonCreditSnapshot> Crew { get; init; } = [];')) {
    if (-not $snapshot.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata snapshot is missing general-media field: $required"
    }
}

foreach ($required in @(
    'Genres = subject.Genres.ToList()',
    'ProductionCompanies =',
    'OriginCountryCodes =',
    'RuntimeMinutes = subject.RuntimeMinutes',
    'ProductionStatus = subject.Status',
    'OriginalLanguage = subject.OriginalLanguage',
    'Cast = subject.Cast',
    'Crew = subject.Crew')) {
    if (-not $service.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata service does not map TMDB general-media detail: $required"
    }
}

foreach ($required in @(
    'BuildMetadataSummary(',
    'ApplyMetadataCredits(',
    'HasMetadataCredits(',
    'MetadataCrewPriority(',
    'MediaCategoryKind.Anime')) {
    if (-not $detail.Contains($required, [StringComparison]::Ordinal)) {
        throw "Detail page does not consume general-media metadata: $required"
    }
}

foreach ($column in @(
    'Genres',
    'RuntimeMinutes',
    'ProductionCompanies',
    'OriginCountryCodes',
    'ProductionStatus',
    'OriginalLanguage',
    'CastCount',
    'CrewCount')) {
    if (-not $catalogView.Contains($column, [StringComparison]::Ordinal)) {
        throw "Recognition report is missing general-media diagnostic column: $column"
    }
}

Write-Host 'Eizo 0.5.3 TMDB details Stage 2 Slice C contract PASS.'
