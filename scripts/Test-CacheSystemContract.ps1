param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-RepoFile([string] $relativePath) {
    return Get-Content -LiteralPath (Join-Path $repoRoot $relativePath) -Raw
}

function Assert-Contains(
    [string] $text,
    [string] $value,
    [string] $message) {
    if (-not $text.Contains($value, [StringComparison]::Ordinal)) {
        throw $message
    }
}

$cacheView = Read-RepoFile 'src/Eizo.App/Views/CacheView.xaml'
$cacheCode = Read-RepoFile 'src/Eizo.App/Views/CacheView.xaml.cs'
$appProject = Read-RepoFile 'src/Eizo.App/Eizo.App.csproj'
$bangumiProject = Read-RepoFile 'src/Eizo.Bangumi/Eizo.Bangumi.csproj'
$bangumiCache = Read-RepoFile 'src/Eizo.Bangumi/BangumiCacheStore.cs'
$subtitleCache = Read-RepoFile 'src/Eizo.App/Playback/ExternalSubtitleService.cs'
$settings = Read-RepoFile 'src/Eizo.App/AppSettingsStore.cs'
$webDavProvider = Read-RepoFile 'src/Eizo.App/Models/WebDavMediaSourceProvider.cs'
$webDavCache = Read-RepoFile 'src/Eizo.App/Models/WebDavCachedRandomAccessSource.cs'
$webDavDiagnostics = Read-RepoFile 'src/Eizo.App/Models/WebDavCacheDiagnostics.cs'
$webDavVideoCache = Read-RepoFile 'src/Eizo.App/Models/WebDavVideoCacheService.cs'
$downloadManager = Read-RepoFile 'src/Eizo.App/Models/VideoCacheDownloadManager.cs'
$cachedPlayback = Read-RepoFile 'src/Eizo.App/Models/CachedVideoRandomAccessSource.cs'
$detailView = Read-RepoFile 'src/Eizo.App/Views/DetailView.xaml'
$detailCode = Read-RepoFile 'src/Eizo.App/Views/DetailView.xaml.cs'
$mainWindow = Read-RepoFile 'src/Eizo.App/MainWindow.xaml.cs'
$playbackPin = Read-RepoFile 'eng/Eizo.Playback.json'

foreach ($placeholder in @('8.6 GB', '6.9 GB', '1.7 GB', '26.9%')) {
    if ($cacheView.Contains($placeholder, [StringComparison]::Ordinal)) {
        throw "Cache page still contains placeholder value: $placeholder"
    }
}

Assert-Contains $cacheView 'x:Name="OverviewUsedText"' 'Cache overview is not bound to real usage.'
Assert-Contains $cacheView 'x:Name="CacheUsageProgress"' 'Cache usage progress is not bindable.'
Assert-Contains $cacheView 'ClearApplicationCacheButton_Click' 'One-click app-cache cleanup is not wired.'
Assert-Contains $cacheView 'x:Name="ApplicationCacheValue"' 'App cache is not summarized as a single item.'
Assert-Contains $cacheView 'x:Name="VideoCacheTitle"' 'Video cache is not the primary cache-content list.'
Assert-Contains $cacheView 'ItemClick="CacheList_ItemClick"' 'Completed video-cache cards are not clickable.'
Assert-Contains $cacheView '<Grid.ContextFlyout>' 'Video-cache cards do not expose a right-click context menu.'
Assert-Contains $cacheView 'DeleteCacheItem_Click' 'Video-cache context menu is missing delete.'
Assert-Contains $cacheCode 'CacheRuntime.Store.GetSnapshotAsync' 'Cache page does not read the unified cache store.'
Assert-Contains $cacheCode 'CacheRuntime.EnforcePolicyAsync' 'Cache page does not apply the cache policy.'
Assert-Contains $appProject '../Eizo.Cache/Eizo.Cache.csproj' 'Eizo.App does not reference Eizo.Cache.'
Assert-Contains $bangumiProject '../Eizo.Cache/Eizo.Cache.csproj' 'Eizo.Bangumi does not reference Eizo.Cache.'
Assert-Contains $bangumiCache 'DiskCacheStore' 'Bangumi cache is not using the unified disk cache.'
Assert-Contains $subtitleCache 'CacheCategory.Subtitles' 'Remote subtitle cache is not classified in the unified cache.'
Assert-Contains $subtitleCache 'CacheRuntime.Store.WriteBytesAsync' 'Remote subtitle cache still bypasses the unified cache store.'
Assert-Contains $subtitleCache 'TryMigrateLegacySubtitleAsync' 'Legacy subtitle cache migration is missing.'
Assert-Contains $settings 'CacheAutoCleanup' 'Cache policy is not persisted.'
Assert-Contains $settings 'RemotePrecacheBytes' 'Remote pre-cache policy is not persisted.'
Assert-Contains $webDavProvider 'DownloadRangeAsync' 'WebDAV provider does not expose byte-range downloads.'
Assert-Contains $webDavProvider 'TryGetKnownRangeSupport' 'WebDAV provider does not retain verified range capability for queue playback.'
Assert-Contains $webDavCache 'IPlaybackRandomAccessSource' 'WebDAV cache is not exposed as a playback random-access source.'
Assert-Contains $webDavCache 'CacheCategory.Media' 'WebDAV media blocks are not stored in the unified media cache.'
Assert-Contains $webDavCache 'TrimGroupAsync' 'Per-media cache working-set trimming is missing.'
Assert-Contains $webDavCache 'RecordDiskHit' 'WebDAV disk-cache hit diagnostics are missing.'
Assert-Contains $webDavCache 'RecordRangeDownload' 'WebDAV range-download diagnostics are missing.'
Assert-Contains $webDavDiagnostics 'HitRate' 'WebDAV cache hit-rate diagnostics are missing.'
Assert-Contains $webDavVideoCache 'SetGroupPinnedAsync' 'Explicit video caching does not pin a completed media group.'
Assert-Contains $webDavVideoCache 'WebDavMediaCacheKeys.BlockCount' 'Explicit video caching does not download the full media block set.'
Assert-Contains $detailView 'CacheEpisodeButton_Click' 'Episode cards do not expose the cache action.'
Assert-Contains $detailCode 'VideoCacheDownloadManager.Default.StartAsync' 'Episode cache action is not registered with the global video-download manager.'
Assert-Contains $downloadManager 'WebDavVideoCacheProgress' 'Video download manager does not consume block-level progress.'
Assert-Contains $downloadManager 'DeleteTaskAsync' 'Downloading video tasks cannot be canceled and deleted.'
Assert-Contains $downloadManager 'DeleteGroupAsync' 'Completed video-cache groups cannot be deleted.'
Assert-Contains $cacheCode 'VideoCacheDownloadManager.Default.Snapshot' 'Cache page does not merge live download tasks.'
Assert-Contains $cacheCode 'PlaybackRequested' 'Cache page does not expose completed-card playback.'
Assert-Contains $cachedPlayback 'IPlaybackRandomAccessSource' 'Completed video cache does not expose a cache-only playback source.'
Assert-Contains $cachedPlayback 'CacheRuntime.Store.ReadBytesAsync' 'Completed video playback does not read from local cache blocks.'
Assert-Contains $mainWindow 'PlaybackSource.FromRandomAccess' 'WebDAV playback is not wired to the cache-backed random-access source.'
Assert-Contains $mainWindow 'TryGetKnownRangeSupport' 'Playback queue does not reuse verified WebDAV range capability.'
Assert-Contains $mainWindow 'OpenCachedVideo' 'Main window does not handle completed cache-card playback.'
Assert-Contains $mainWindow 'CachedVideoRandomAccessSource' 'Completed cache-card playback still depends on the network data path.'
Assert-Contains $playbackPin '"version": "0.2.2"' 'Eizo is not pinned to Playback 0.2.2.'
if ($playbackPin.Contains('"patch"', [StringComparison]::Ordinal)) {
    throw 'Playback pin still relies on the legacy Eizo-local source patch.'
}

Write-Host 'Cache system contract PASS.'
