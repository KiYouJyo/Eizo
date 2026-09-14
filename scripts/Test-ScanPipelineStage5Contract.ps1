param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required scan-pipeline file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$store = Read-Text 'src/Eizo.App/Models/MediaCatalogStore.cs'
$refresh = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataRefreshPolicy.cs'
$snapshot = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataSnapshot.cs'
$catalogView = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'
$coordinator = Read-Text 'src/Eizo.App/Models/MediaScanCoordinator.cs'

$scanStart = $store.IndexOf(
    'public async Task<int> ScanSourceAsync(',
    [StringComparison]::Ordinal)
$reuse = $store.IndexOf(
    'ReuseResolvedMetadata(source.Id, discovered);',
    $scanStart,
    [StringComparison]::Ordinal)
$enrich = $store.IndexOf(
    'await EnrichMetadataAsync(',
    $reuse,
    [StringComparison]::Ordinal)
$commit = $store.IndexOf(
    'CommitSourceScan(source.Id, discovered);',
    $enrich,
    [StringComparison]::Ordinal)
$changed = $store.IndexOf(
    'Changed?.Invoke(this, EventArgs.Empty);',
    $commit,
    [StringComparison]::Ordinal)

if ($scanStart -lt 0 -or
    $reuse -lt $scanStart -or
    $enrich -lt $reuse -or
    $commit -lt $enrich -or
    $changed -lt $commit) {
    throw 'Scan pipeline must remain Discover -> Reuse -> Metadata -> Commit -> Changed.'
}

foreach ($required in @(
    'MediaMetadataRefreshPolicy.Select(',
    'MediaMetadataRefreshPolicy.MarkReused(',
    'IdentityBindings.GetHint(',
    'IdentityBindings.UpsertAutomatic(',
    'forceRefresh: true',
    'SchemaVersion: 3',
    'SchemaVersion: 1 or 2 or 3')) {
    if (-not $store.Contains($required, [StringComparison]::Ordinal)) {
        throw "Scan/persistence lifecycle is missing: $required"
    }
}

foreach ($required in @(
    'MetadataAutoScrapeOnScan',
    'var metadataService =',
    'MediaCatalogStore.Default.ScanSourceAsync(',
    'metadataService,',
    'progress => UpdateProgress(started, progress)')) {
    if (-not $coordinator.Contains($required, [StringComparison]::Ordinal)) {
        throw "Scan coordinator is not using the single scan-time Metadata pipeline: $required"
    }
}

$runAsyncStart = $coordinator.IndexOf(
    'private async Task<MediaScanSnapshot> RunAsync(',
    [StringComparison]::Ordinal)
$runMetadataStart = $coordinator.IndexOf(
    'private async Task<MediaScanSnapshot> RunMetadataAsync(',
    [StringComparison]::Ordinal)
$runAsyncBody = $coordinator.Substring(
    $runAsyncStart,
    $runMetadataStart - $runAsyncStart)
if ($runAsyncBody.Contains(
        'StartMetadataAsync(',
        [StringComparison]::Ordinal)) {
    throw 'Automatic scanning must not launch a second post-commit Metadata job.'
}

foreach ($required in @(
    'RetainedLastKnownGood',
    'ShouldRetainExisting(',
    'IsTransportFailure(',
    'forceRefresh')) {
    if (-not $refresh.Contains($required, [StringComparison]::Ordinal)) {
        throw "Last-known-good refresh policy is missing: $required"
    }
}

foreach ($required in @(
    'public string? RefreshState { get; init; }',
    'public DateTimeOffset? LastRefreshAttemptUtc { get; init; }',
    'public List<MetadataProviderErrorSnapshot> RefreshErrors { get; init; }')) {
    if (-not $snapshot.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata refresh diagnostics are missing: $required"
    }
}

foreach ($column in @(
    'MetadataRefreshState',
    'LastRefreshAttemptUtc',
    'RefreshErrors')) {
    if (-not $catalogView.Contains($column, [StringComparison]::Ordinal)) {
        throw "Recognition report is missing refresh diagnostic column: $column"
    }
}

Write-Host 'Eizo 0.5.8 scan/scrape/persistence pipeline contract PASS.'
