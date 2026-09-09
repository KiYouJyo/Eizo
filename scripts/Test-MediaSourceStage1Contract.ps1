$ErrorActionPreference = 'Stop'

$catalogStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaCatalogStore.cs' -Raw
$sourceStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaSourceStore.cs' -Raw
$providerContract = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaSourceProvider.cs' -Raw
$sourcesView = Get-Content -LiteralPath 'src/Eizo.App/Views/SourcesView.xaml.cs' -Raw
$catalogView = Get-Content -LiteralPath 'src/Eizo.App/Views/CatalogView.xaml.cs' -Raw
$mainWindow = Get-Content -LiteralPath 'src/Eizo.App/MainWindow.xaml.cs' -Raw

foreach ($demoTitle in @(
    '葬送的芙莉莲',
    '胆大党',
    '间谍过家家',
    'Sample Movie',
    'OneDrive · Personal',
    'WebDAV · NAS')) {
    if ($catalogStore.Contains($demoTitle) -or $sourcesView.Contains($demoTitle)) {
        throw "Stage 1 contract violation: demo data remains in a real-data store/view: $demoTitle"
    }
}

if ($catalogStore -notmatch '"catalog\.json"' -or
    $sourceStore -notmatch '"sources\.json"') {
    throw 'Stage 1 contract violation: media sources and catalog must be persisted under Eizo local app data.'
}

if ($catalogStore -notmatch 'SchemaVersion:\s*1' -or
    $sourceStore -notmatch 'SchemaVersion:\s*1') {
    throw 'Stage 1 contract violation: source/catalog persistence must be schema-versioned before WebDAV fields are added.'
}

if ($sourceStore -match 'Password' -or
    $sourceStore -notmatch 'CredentialKey') {
    throw 'Stage 1 contract violation: source metadata may keep only a credential reference; secrets must not be persisted in sources.json.'
}

if ($catalogStore -notmatch 'RegisterLocalFile' -or
    $catalogStore -notmatch 'ScanLocalSource' -or
    $catalogStore -notmatch 'IsSupportedVideoPath') {
    throw 'Stage 1 contract violation: real local video registration/scanning contract is incomplete.'
}

if ($catalogView -notmatch 'EventHandler<CatalogMediaItemModel>\? MediaRequested' -or
    $mainWindow -notmatch 'OpenCatalogMediaAsync\s*\(\s*CatalogMediaItemModel item\s*\)') {
    throw 'Stage 1 contract violation: total catalog must invoke real media items instead of demo titles.'
}

if ($sourcesView -notmatch 'MediaSourceStore\.Default' -or
    $sourcesView -notmatch 'MediaCatalogStore\.Default') {
    throw 'Stage 1 contract violation: SourcesView must be driven by real source/catalog stores.'
}

if ($providerContract -notmatch 'interface IMediaSourceProvider' -or
    $providerContract -notmatch 'class LocalMediaSourceProvider' -or
    $sourceStore -notmatch 'WebDav') {
    throw 'Stage 1 contract violation: provider abstraction and WebDAV source kind must exist before protocol code is introduced.'
}

Write-Host 'WebDAV Stage 1 media-source contract PASS.'
