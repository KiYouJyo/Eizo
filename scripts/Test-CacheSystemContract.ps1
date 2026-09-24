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
$cardStyles = Read-RepoFile 'src/Eizo.App/Themes/CardStyles.xaml'
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
Assert-Contains $cacheView 'ContainerContentChanging="CacheList_ContainerContentChanging"' 'Video-cache cards do not attach a whole-card right-click context menu.'
Assert-Contains $cacheCode 'CacheList_ContainerContentChanging' 'Video-cache context menu is not generated from the item container.'
Assert-Contains $cacheCode 'OpenCacheFolder_Click' 'Video-cache context menu is missing open-folder.'
Assert-Contains $cacheCode 'ResolveCacheFolderPath' 'Video-cache open-folder action does not resolve cached file locations.'
Assert-Contains $cacheCode 'DeleteCacheItem_Click' 'Video-cache context menu is missing delete.'
Assert-Contains $cacheCode 'DangerMenuFlyoutItemStyle' 'Video-cache delete action does not use the shared destructive style.'
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
Assert-Contains $webDavVideoCache 'ImportFileAsync' 'Explicit video caching does not commit a completed single media file.'
Assert-Contains $webDavVideoCache 'BuildManualGroupKey' 'Explicit video caching is not isolated from automatic playback cache groups.'
Assert-Contains $webDavVideoCache 'WebDavMediaCacheKeys.BlockCount' 'Explicit video caching does not stream the complete media range.'
if ($webDavVideoCache.Contains(
        'CacheRuntime.Store.WriteBytesAsync',
        [StringComparison]::Ordinal)) {
    throw 'Manual video caching must not write per-range .blk cache entries.'
}
Assert-Contains $detailView 'CacheEpisodeButton_Click' 'Episode cards do not expose the cache action.'
Assert-Contains $detailView 'Tag="{x:Bind Self}"' 'Episode cache action does not carry the episode number context.'
Assert-Contains $detailCode 'VideoCacheDownloadManager.Default.StartAsync' 'Episode cache action is not registered with the global video-download manager.'
Assert-Contains $downloadManager 'WebDavVideoCacheProgress' 'Video download manager does not consume block-level progress.'
Assert-Contains $downloadManager 'DeleteTaskAsync' 'Downloading video tasks cannot be canceled and deleted.'
Assert-Contains $downloadManager 'DeleteGroupAsync' 'Completed video-cache groups cannot be deleted.'
Assert-Contains $downloadManager 'TogglePause' 'Video-cache downloads cannot be paused/resumed.'
Assert-Contains $downloadManager 'VideoCacheDownloadStatus.Paused' 'Paused download state is missing.'
Assert-Contains $downloadManager 'BytesPerSecond' 'Video-cache download speed is not tracked.'
Assert-Contains $downloadManager 'SmoothedBytesPerSecond' 'Video-cache speed does not use a stable rolling sample.'
Assert-Contains $downloadManager 'string? displayMeta = null' 'Video-cache download tasks do not retain episode display metadata.'
Assert-Contains $webDavVideoCache 'waitForResume' 'Block download loop does not honor the pause gate.'
Assert-Contains $webDavVideoCache 'string? displayMeta = null' 'Episode display metadata is not persisted into video-cache entries.'
Assert-Contains $webDavVideoCache 'NetworkDownloadedBytes' 'Video-cache speed cannot distinguish network bytes from reused cache blocks.'
Assert-Contains $cacheCode 'VideoCacheDownloadManager.Default.Snapshot' 'Cache page does not merge live download tasks.'
Assert-Contains $cacheCode 'PlaybackRequested' 'Cache page does not expose completed-card playback.'
Assert-Contains $cacheCode 'existing.UpdateFrom(candidate)' 'Cache cards are rebuilt instead of updated in place.'
Assert-Contains $cacheCode 'VideoCacheDownloadManager.Default.TogglePause' 'Clicking an active cache card does not pause/resume it.'
Assert-Contains $cacheCode 'ResolveCachedEpisodeLabel' 'Legacy completed video-cache cards cannot recover episode numbers from the media catalog.'
Assert-Contains $cacheCode 'FormatSpeed' 'Cache page does not format live download speed.'
Assert-Contains $cacheView 'SpeedText' 'Cache cards do not display download speed.'
Assert-Contains $cacheView 'InverseBooleanToVisibilityConverter' 'Completed cache cards do not hide the progress region.'
if ($cacheCode.Contains('_items.Clear()', [StringComparison]::Ordinal)) {
    throw 'Cache page still clears the whole item collection during progress updates.'
}
Assert-Contains $cacheView 'ItemContainerStyle="{StaticResource CacheCardListItemStyle}"' 'Video-cache list does not use the shared card style.'
Assert-Contains $cardStyles 'x:Key="CacheCardListItemStyle"' 'Shared cache-card style is missing.'
Assert-Contains $cardStyles 'CardBackgroundFillColorDefaultBrush' 'Video-cache rows do not have a card background.'
Assert-Contains $cardStyles 'Property="CornerRadius"' 'Video-cache item highlight does not share the card corner radius.'
Assert-Contains $cardStyles '<ControlTemplate TargetType="ListViewItem">' 'Video-cache cards still rely on the default ListViewItem surface.'
Assert-Contains $cardStyles 'UseSystemFocusVisuals' 'Default outer focus visuals can still draw around cache cards.'
Assert-Contains $cardStyles 'Property="BorderThickness"' 'Cache-card template does not explicitly control the outer border.'
Assert-Contains $cardStyles 'x:Key="DangerMenuFlyoutItemStyle"' 'Shared destructive menu style is missing.'
Assert-Contains $cacheView 'Mode=OneWay' 'Video-cache card bindings are not live.'
Assert-Contains $cachedPlayback 'IPlaybackRandomAccessSource' 'Completed video cache does not expose a cache-only playback source.'
Assert-Contains $cachedPlayback 'CacheRuntime.Store.ReadBytesAsync' 'Legacy completed block-cache playback support is missing.'
Assert-Contains $mainWindow 'PlaybackSource.FromFile' 'Single-file manual video cache playback is not wired.'
Assert-Contains $mainWindow 'PlaybackSource.FromRandomAccess' 'WebDAV playback is not wired to the cache-backed random-access source.'
Assert-Contains $mainWindow 'TryGetKnownRangeSupport' 'Playback queue does not reuse verified WebDAV range capability.'
Assert-Contains $mainWindow 'OpenCachedVideo' 'Main window does not handle completed cache-card playback.'
Assert-Contains $mainWindow 'CachedVideoRandomAccessSource' 'Completed cache-card playback still depends on the network data path.'
Assert-Contains $playbackPin '"version": "0.2.3"' 'Eizo is not pinned to Playback 0.2.3.'
if ($playbackPin.Contains('"patch"', [StringComparison]::Ordinal)) {
    throw 'Playback pin still relies on the legacy Eizo-local source patch.'
}

Write-Host 'Cache system contract PASS.'
