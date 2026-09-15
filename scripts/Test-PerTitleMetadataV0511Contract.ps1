param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required per-title metadata file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$store = Read-Text 'src/Eizo.App/Models/MediaCatalogStore.cs'
$coordinator = Read-Text 'src/Eizo.App/Models/MediaScanCoordinator.cs'
$actions = Read-Text 'src/Eizo.App/Models/CatalogSubjectMetadataActions.cs'
$dialog = Read-Text 'src/Eizo.App/Views/MetadataMatchDialog.cs'
$catalog = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'
$detail = Read-Text 'src/Eizo.App/Views/DetailView.xaml.cs'

foreach ($required in @(
    'public Task<int> ScrapeMediaItemsMetadataAsync(',
    'public Task<int> ScrapeItemMetadataAsync(',
    'ScrapeTargetMetadataAsync(',
    'CommitTargetedMetadata(',
    'LocationKey(')) {
    if (-not $store.Contains($required, [StringComparison]::Ordinal)) {
        throw "Targeted metadata store contract is incomplete: $required"
    }
}

if (-not $store.Contains(
        'public async Task<int> ScrapeSourceMetadataAsync(',
        [StringComparison]::Ordinal)) {
    throw 'Source-wide scrape must remain available for the Media Sources page.'
}

foreach ($required in @(
    'public async Task<int> ScrapeSubjectMetadataAsync(',
    'public async Task<int> ScrapeItemMetadataAsync(')) {
    if (-not $coordinator.Contains($required, [StringComparison]::Ordinal)) {
        throw "Coordinator targeted metadata API is incomplete: $required"
    }
}

foreach ($required in @(
    'CatalogSubjectMetadataActions',
    'ScrapeSubjectMetadataAsync(',
    'SetManualIdentityBinding(',
    'ClearManualIdentityBinding(',
    'FindRefreshedSubject(')) {
    if (-not $actions.Contains($required, [StringComparison]::Ordinal)) {
        throw "Shared per-title metadata action is incomplete: $required"
    }
}

foreach ($required in @(
    'MetadataMatchDialog',
    'SearchMetadataMatchesAsync(',
    'Bangumi',
    'TMDB')) {
    if (-not $dialog.Contains($required, [StringComparison]::Ordinal)) {
        throw "Shared manual-match dialog is incomplete: $required"
    }
}

foreach ($required in @(
    'AddSubjectMetadataActions(',
    'SubjectRefresh_Click',
    'SubjectManualMatch_Click',
    'SubjectClearManualMatch_Click',
    '重新刮削',
    '手动匹配')) {
    if (-not $catalog.Contains($required, [StringComparison]::Ordinal)) {
        throw "Library-card metadata actions are incomplete: $required"
    }
}

foreach ($required in @(
    'CatalogSubjectMetadataActions',
    '.RefreshAsync(',
    'MetadataMatchDialog.ShowAsync(',
    '.ApplyManualMatchAsync(',
    '.ClearManualMatchAsync(')) {
    if (-not $detail.Contains($required, [StringComparison]::Ordinal)) {
        throw "Detail-page actions are not routed through per-title APIs: $required"
    }
}

if ($actions.Contains(
        'StartMetadataAsync(',
        [StringComparison]::Ordinal)) {
    throw 'Per-title metadata actions must not fall back to source-wide StartMetadataAsync.'
}

Write-Host 'Eizo 0.5.11 per-title scrape and library actions contract PASS.'
