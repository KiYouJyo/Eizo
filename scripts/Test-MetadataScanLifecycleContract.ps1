$ErrorActionPreference = 'Stop'

$app = Get-Content -LiteralPath 'src/Eizo.App/App.xaml.cs' -Raw
$catalog = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaCatalogStore.cs' -Raw
$scanCoordinator = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaScanCoordinator.cs' -Raw
$scanModels = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaScanModels.cs' -Raw
$sourcesView = Get-Content -LiteralPath 'src/Eizo.App/Views/SourcesView.xaml.cs' -Raw
$sourcesXaml = Get-Content -LiteralPath 'src/Eizo.App/Views/SourcesView.xaml' -Raw
$catalogView = Get-Content -LiteralPath 'src/Eizo.App/Views/CatalogView.xaml.cs' -Raw
$uiModels = Get-Content -LiteralPath 'src/Eizo.App/Models/UiModels.cs' -Raw

if (Test-Path 'src/Eizo.App/Models/MetadataEnrichmentCoordinator.cs') {
    throw 'Metadata scan-lifecycle contract violation: persistent MetadataEnrichmentCoordinator must be removed.'
}

foreach ($forbidden in @(
    'StartMetadataEnrichment',
    'MetadataEnrichmentCoordinator',
    'MetadataEnrichmentRequested')) {
    if ($app -match [regex]::Escape($forbidden) -or
        $catalog -match [regex]::Escape($forbidden)) {
        throw "Metadata scan-lifecycle contract violation: obsolete background hook remains: $forbidden"
    }
}

foreach ($required in @(
    'MediaMetadataService',
    'ScanSourceAsync(',
    'StartMetadataAsync(',
    'ScrapeSourceMetadataAsync(',
    '_metadataService.Value',
    'IsScraping(',
    'MediaScanStage.Metadata',
    'MetadataResolved',
    'MetadataUnresolved',
    'MetadataErrors')) {
    if ($scanCoordinator -notmatch [regex]::Escape($required) -and
        $scanModels -notmatch [regex]::Escape($required)) {
        throw "Metadata scan-lifecycle contract missing: $required"
    }
}

foreach ($required in @(
    'EnrichMetadataAsync',
    'ScrapeSourceMetadataAsync',
    'forceRefresh: true',
    'ReuseResolvedMetadata',
    'CreateMetadataFailureSnapshot',
    'CommitSourceScan',
    'Changed?.Invoke(this, EventArgs.Empty)',
    'MediaMetadataStatus.Unresolved',
    'MediaMetadataStatus.Error')) {
    if ($catalog -notmatch [regex]::Escape($required)) {
        throw "Metadata source-scan contract missing: $required"
    }
}

$scanRunStart = $scanCoordinator.IndexOf('private async Task<MediaScanSnapshot> RunAsync(')
$metadataRunStart = $scanCoordinator.IndexOf('private async Task<MediaScanSnapshot> RunMetadataAsync(')
if ($scanRunStart -lt 0 -or $metadataRunStart -le $scanRunStart) {
    throw 'Metadata source-operation contract violation: scan and scrape runners must be separate.'
}

$scanRun = $scanCoordinator.Substring(
    $scanRunStart,
    $metadataRunStart - $scanRunStart)
if ($scanRun -match [regex]::Escape('_metadataService.Value')) {
    throw 'Metadata source-operation contract violation: discovery scan must not invoke Metadata.'
}

$metadataRun = $scanCoordinator.Substring($metadataRunStart)
if ($metadataRun -notmatch [regex]::Escape('ScrapeSourceMetadataAsync(') -or
    $metadataRun -notmatch [regex]::Escape('_metadataService.Value')) {
    throw 'Metadata source-operation contract violation: scrape runner must invoke the Metadata pipeline.'
}

if ($sourcesView -notmatch '_sourceItems' -or
    $sourcesView -notmatch 'UpdateFrom\(next\)' -or
    $sourcesView -match 'SourceList\.ItemsSource\s*=\s*_sources') {
    throw 'Source-card contract violation: scan progress must update stable view models instead of replacing the ListView ItemsSource.'
}

if ($uiModels -notmatch 'INotifyPropertyChanged' -or
    $sourcesXaml -notmatch 'Summary, Mode=OneWay' -or
    $sourcesXaml -notmatch 'IsScanning, Mode=OneWay' -or
    $sourcesXaml -notmatch 'IsScraping, Mode=OneWay' -or
    $sourcesXaml -notmatch 'CanScrape, Mode=OneWay' -or
    $sourcesXaml -notmatch 'ScrapeSourceButton_Click') {
    throw 'Source-card contract violation: scan/scrape progress properties must support independent in-place actions.'
}

foreach ($column in @(
    'MetadataRuntimeVersion',
    'MetadataRecognitionRuntimeVersion',
    'MetadataStatus',
    'MetadataProvider',
    'MetadataSubjectId',
    'MetadataConfidence',
    'MetadataExternalIds',
    'MetadataErrors',
    'MetadataUpdatedAtUtc')) {
    if ($catalogView -notmatch [regex]::Escape($column)) {
        throw "Unified Recognition/Metadata report missing column: $column"
    }
}

Write-Host 'Eizo v0.3.7 independent scan/scrape lifecycle and unified diagnostics contract PASS.'
