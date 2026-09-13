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
$mainWindow = Read-RepoFile 'src/Eizo.App/MainWindow.xaml.cs'
$playbackPin = Read-RepoFile 'eng/Eizo.Playback.json'

foreach ($placeholder in @('8.6 GB', '6.9 GB', '1.7 GB', '26.9%')) {
    if ($cacheView.Contains($placeholder, [StringComparison]::Ordinal)) {
        throw "Cache page still contains placeholder value: $placeholder"
    }
}

Assert-Contains $cacheView 'x:Name="OverviewUsedText"' 'Cache overview is not bound to real usage.'
Assert-Contains $cacheView 'x:Name="CacheUsageProgress"' 'Cache usage progress is not bindable.'
Assert-Contains $cacheView 'ClearCacheButton_Click' 'Clear-cache action is not wired.'
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
Assert-Contains $webDavCache 'IPlaybackRandomAccessSource' 'WebDAV cache is not exposed as a playback random-access source.'
Assert-Contains $webDavCache 'CacheCategory.Media' 'WebDAV media blocks are not stored in the unified media cache.'
Assert-Contains $webDavCache 'TrimGroupAsync' 'Per-media cache working-set trimming is missing.'
Assert-Contains $mainWindow 'PlaybackSource.FromRandomAccess' 'WebDAV playback is not wired to the cache-backed random-access source.'
Assert-Contains $playbackPin '"version": "0.2.2"' 'Eizo is not pinned to Playback 0.2.2.'
if ($playbackPin.Contains('"patch"', [StringComparison]::Ordinal)) {
    throw 'Playback pin still relies on the legacy Eizo-local source patch.'
}

Write-Host 'Cache system contract PASS.'
