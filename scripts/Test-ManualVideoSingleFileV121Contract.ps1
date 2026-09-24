param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required manual-video cache file is missing: $relativePath"
    }

    return [IO.File]::ReadAllText(
        $path,
        [Text.Encoding]::UTF8)
}

function Assert-Contains(
    [string]$text,
    [string]$needle,
    [string]$message) {
    if (-not $text.Contains(
            $needle,
            [StringComparison]::Ordinal)) {
        throw $message
    }
}

$manual = Read-Text 'src/Eizo.App/Models/WebDavVideoCacheService.cs'
$keys = Read-Text 'src/Eizo.App/Models/WebDavMediaCacheKeys.cs'
$automatic = Read-Text 'src/Eizo.App/Models/WebDavCachedRandomAccessSource.cs'
$manager = Read-Text 'src/Eizo.App/Models/VideoCacheDownloadManager.cs'
$uiModels = Read-Text 'src/Eizo.App/Models/UiModels.cs'
$cacheView = Read-Text 'src/Eizo.App/Views/CacheView.xaml.cs'
$mainWindow = Read-Text 'src/Eizo.App/MainWindow.xaml.cs'
$store = Read-Text 'src/Eizo.Cache/DiskCacheStore.cs'
$tests = Read-Text 'tests/Eizo.Cache.Tests/DiskCacheStoreTests.cs'

foreach ($required in @(
    'BuildManualGroupKey',
    'BuildManualFileKey',
    'manual-video:',
    'TryParseAnyGroupKey',
    'IsManualGroupKey')) {
    Assert-Contains $keys $required "Manual/automatic cache key separation is missing: $required"
}

foreach ($required in @(
    'ManualStagingFolder',
    'manual-video-staging',
    'ImportFileAsync',
    'ResolveMediaExtension',
    'Path.GetExtension',
    'Pinned: true',
    'GroupKey: manualGroupKey',
    'automaticGroupKey',
    'CacheRuntime.Store.ReadBytesAsync',
    'webDavProvider.DownloadRangeAsync',
    'FileStream(',
    'FileOptions.SequentialScan')) {
    Assert-Contains $manual $required "Single-file manual cache pipeline is incomplete: $required"
}

foreach ($forbidden in @(
    'CacheRuntime.Store.WriteBytesAsync',
    '".blk"',
    'SetGroupPinnedAsync')) {
    if ($manual.Contains(
            $forbidden,
            [StringComparison]::Ordinal)) {
        throw "Manual video cache still writes block-cache storage: $forbidden"
    }
}

foreach ($required in @(
    'CacheRuntime.Store.WriteBytesAsync',
    '".blk"',
    'BuildBlockKey',
    'TrimGroupAsync')) {
    Assert-Contains $automatic $required "Automatic playback cache changed unexpectedly: $required"
}

foreach ($required in @(
    'ImportFileAsync(',
    'File.Move(',
    'File.Copy(',
    'ToSnapshot(')) {
    Assert-Contains $store $required "Disk cache single-file import support is missing: $required"
}

foreach ($required in @(
    'string? LocalPath',
    'LocalPath = result.Path')) {
    $haystack = $manager + $uiModels
    Assert-Contains $haystack $required "Manual cached-file path is not propagated: $required"
}

foreach ($required in @(
    'task.LocalPath',
    'IsManualGroupKey(',
    'TryParseAnyGroupKey(',
    'entry.Path')) {
    Assert-Contains $cacheView $required "Cache UI does not surface the manual video file correctly: $required"
}

foreach ($required in @(
    'request.LocalPath',
    'PlaybackSource.FromFile(',
    'CachedVideoRandomAccessSource(',
    'IsManualGroupKey(')) {
    Assert-Contains $mainWindow $required "Cached playback is missing single-file/legacy compatibility: $required"
}

foreach ($required in @(
    'ImportFile_StoresSinglePinnedMediaFile',
    'Assert.Equal(".mkv", Path.GetExtension(entry.Path))')) {
    Assert-Contains $tests $required "Single-file cache regression test is missing: $required"
}

Write-Host 'Eizo v1.2.1 manual video single-file cache contract PASS.'
