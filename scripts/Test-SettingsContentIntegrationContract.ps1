param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Read-Text([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing settings integration file: $path"
    }
    return Get-Content -LiteralPath $path -Raw
}

$settingsXaml = Read-Text 'src/Eizo.App/Views/SettingsView.xaml'
$settingsCode = Read-Text 'src/Eizo.App/Views/SettingsView.xaml.cs'
$appSettings = Read-Text 'src/Eizo.App/AppSettingsStore.cs'
$scanCoordinator = Read-Text 'src/Eizo.App/Models/MediaScanCoordinator.cs'
$player = Read-Text 'src/Eizo.App/Views/PlayerView.xaml.cs'
$trackStore = Read-Text 'src/Eizo.App/Models/PlaybackTrackPreferenceStore.cs'

foreach ($required in @(
    'GeneralSettingsPanel',
    'MetadataSettingsPanel',
    'PlaybackSettingsPanel',
    'SettingsSectionList_SelectionChanged',
    'MetadataAutoScrapeToggle',
    'MetadataArtworkToggle',
    'MetadataRescrapeButton',
    'MetadataClearCacheButton',
    'AutoPlayNextToggle',
    'RememberPlaybackRateToggle',
    'DefaultPlaybackRateCombo',
    'PreferredAudioLanguageCombo',
    'PreferredSubtitleLanguageCombo',
    'PreferredSecondarySubtitleLanguageCombo',
    'RememberSubtitleTrackToggle',
    'PrimarySubtitlePositionSettingsSlider',
    'SecondarySubtitlePositionSettingsSlider',
    'PrimarySubtitleOpacitySettingsSlider',
    'SecondarySubtitleOpacitySettingsSlider')) {
    if ($settingsXaml -notmatch [regex]::Escape($required) -and
        $settingsCode -notmatch [regex]::Escape($required)) {
        throw "Settings integration contract missing UI/action: $required"
    }
}

foreach ($forbidden in @(
    'Settings_JapaneseMedia',
    'Settings_PreferredTitle',
    'Settings_ShowJapaneseTitle',
    'Settings_ShowRomaji')) {
    if ($settingsXaml -match [regex]::Escape($forbidden) -or
        $settingsCode -match [regex]::Escape($forbidden)) {
        throw "Settings integration still exposes removed text-only/Japanese-media option: $forbidden"
    }
}

foreach ($required in @(
    'MetadataAutoScrapeOnScan',
    'MetadataArtworkEnrichment',
    'AutoPlayNextEpisode',
    'RememberPlaybackRate',
    'DefaultPlaybackRate',
    'LastPlaybackRate',
    'RememberSubtitleTrack',
    'PreferredAudioLanguage',
    'PreferredSubtitleLanguage',
    'PreferredSecondarySubtitleLanguage')) {
    if ($appSettings -notmatch [regex]::Escape($required)) {
        throw "Persisted settings contract missing: $required"
    }
}

$runStart = $scanCoordinator.IndexOf('private async Task<MediaScanSnapshot> RunAsync(')
$metadataStart = $scanCoordinator.IndexOf('private async Task<MediaScanSnapshot> RunMetadataAsync(')
if ($runStart -lt 0 -or $metadataStart -le $runStart) {
    throw 'Settings integration contract could not locate source-scan and explicit metadata runners.'
}
$scanRun = $scanCoordinator.Substring($runStart, $metadataStart - $runStart)
foreach ($required in @(
    'MetadataAutoScrapeOnScan',
    '_metadataService.Value',
    'ScanSourceAsync(',
    'metadataService,')) {
    if ($scanRun -notmatch [regex]::Escape($required)) {
        throw "Auto metadata setting is not wired into the single-commit source scan: $required"
    }
}
if ($scanRun -match [regex]::Escape('StartMetadataAsync(')) {
    throw 'Auto metadata setting must not create a second post-commit metadata job.'
}

$metadataRun = $scanCoordinator.Substring($metadataStart)
if ($metadataRun -notmatch [regex]::Escape('ScrapeSourceMetadataAsync(')) {
    throw 'Manual re-scrape action must remain independent from source discovery.'
}

foreach ($required in @(
    'AutoPlayNextEpisode',
    'ApplyPlaybackRatePreference',
    'ApplyTrackPreferencesAsync',
    'PreferredAudioLanguage',
    'PreferredSubtitleLanguage',
    'PreferredSecondarySubtitleLanguage',
    'automaticSecondaryCandidate',
    'RememberPrimarySubtitlePreference',
    'PlaybackTrackPreferenceStore')) {
    if ($player -notmatch [regex]::Escape($required)) {
        throw "Player settings integration missing runtime hook: $required"
    }
}

foreach ($required in @(
    'SubtitleTrackPreference',
    'BuildSubjectKey',
    'NormalizeLanguage',
    'ProviderSubjectId',
    'track-preferences.json')) {
    if ($trackStore -notmatch [regex]::Escape($required)) {
        throw "Semantic subtitle preference contract missing: $required"
    }
}

Write-Host 'Eizo v0.4.7 settings content/runtime integration contract PASS.'
