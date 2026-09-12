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
    'EnrichMetadataWithTransportRetryAsync',
    'MetadataTransportRetryDelays',
    'MetadataTransportCooldown(',
    'MetadataTransportFailuresBeforeCooldown',
    'CommitSourceScan',
    'Changed?.Invoke(this, EventArgs.Empty)',
    'MediaMetadataStatus.Unresolved',
    'MediaMetadataStatus.Error')) {
    if ($catalog -notmatch [regex]::Escape($required)) {
        throw "Metadata source-scan contract missing: $required"
    }
}

if ($catalog -match 'providerSuspended' -or
    $catalog -match '"ProviderSuspended"') {
    throw 'Metadata transport-resilience contract violation: transient failures must not suspend the provider for the remainder of a library scrape.'
}

if ($catalog -notmatch 'Task\.Delay\(' -or
    $catalog -notmatch 'IsTransportMetadataFailure') {
    throw 'Metadata transport-resilience contract violation: retry/cooldown logic must be driven by transport-failure classification.'
}

$scrapeStart = $catalog.IndexOf('public async Task<int> ScrapeSourceMetadataAsync(')
$scrapeEnd = $catalog.IndexOf(
    'private async Task<CatalogMediaItemModel[]> DiscoverRemoteSourceAsync',
    $scrapeStart)
if ($scrapeStart -lt 0 -or $scrapeEnd -le $scrapeStart) {
    throw 'Metadata scrape-recognition contract violation: standalone scrape method was not found.'
}

$scrapeMethod = $catalog.Substring(
    $scrapeStart,
    $scrapeEnd - $scrapeStart)
$refreshIndex = $scrapeMethod.IndexOf('EnsureRecognitionRuntimeCurrentAsync(')
$snapshotIndex = $scrapeMethod.IndexOf('current = _items')
if ($refreshIndex -lt 0 -or
    $snapshotIndex -lt 0 -or
    $refreshIndex -ge $snapshotIndex) {
    throw 'Metadata scrape-recognition contract violation: Recognition must refresh before the standalone scrape snapshots catalog items.'
}

$recognitionRefreshStart = $catalog.IndexOf(
    'public async Task<int> EnsureRecognitionRuntimeCurrentAsync(')
$itemKeyStart = $catalog.IndexOf(
    'internal static string ItemKey',
    $recognitionRefreshStart)
if ($recognitionRefreshStart -lt 0 -or $itemKeyStart -le $recognitionRefreshStart) {
    throw 'Metadata scrape-recognition contract violation: Recognition refresh method was not found.'
}

$recognitionRefresh = $catalog.Substring(
    $recognitionRefreshStart,
    $itemKeyStart - $recognitionRefreshStart)
if ($recognitionRefresh -notmatch 'Metadata\s*=\s*null') {
    throw 'Metadata scrape-recognition contract violation: refreshing Recognition must invalidate Metadata bound to the stale Recognition snapshot.'
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
