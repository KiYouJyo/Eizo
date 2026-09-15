param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required presentation file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$presentation = Read-Text 'src/Eizo.App/Models/CatalogSubjectPresentation.cs'
$catalog = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'
$detail = Read-Text 'src/Eizo.App/Views/DetailView.xaml.cs'
$detailXaml = Read-Text 'src/Eizo.App/Views/DetailView.xaml'
$episode = Read-Text 'src/Eizo.App/Models/EpisodeDisplayItemModel.cs'

foreach ($required in @(
    'CatalogSubjectPresentation(',
    'ReleaseYear',
    'Genres',
    'RuntimeMinutes',
    'OriginCountryCodes',
    'ProductionCompanies',
    'SourceCount',
    'HasLocalSource',
    'HasRemoteSource')) {
    if (-not $presentation.Contains($required, [StringComparison]::Ordinal)) {
        throw "Unified subject presentation is incomplete: $required"
    }
}

foreach ($required in @(
    'CatalogSubjectPresentation.Create(subject)',
    'presentation.PosterUrl',
    'presentation.SecondaryTitle',
    'presentation.Genres.Take(2)')) {
    if (-not $catalog.Contains($required, [StringComparison]::Ordinal)) {
        throw "Catalog cards are not using the unified presentation: $required"
    }
}

foreach ($required in @(
    'CatalogSubjectPresentation.Create(_subject)',
    'BuildMetadataSummary(presentation)',
    'ApplySeasonContext()',
    'season?.PosterUrl',
    'EpisodeAirDate')) {
    if (-not $detail.Contains($required, [StringComparison]::Ordinal)) {
        throw "Detail UI is not using real presentation data: $required"
    }
}

if (-not $detailXaml.Contains(
        'Text="{x:Bind SecondaryText}"',
        [StringComparison]::Ordinal)) {
    throw 'Episode cards are not binding the real secondary/date text.'
}

foreach ($required in @(
    'public string AirDate { get; init; }',
    'public string SecondaryText =>')) {
    if (-not $episode.Contains($required, [StringComparison]::Ordinal)) {
        throw "Episode presentation model is incomplete: $required"
    }
}

Write-Host 'Eizo 0.5.10 real-content presentation contract PASS.'
