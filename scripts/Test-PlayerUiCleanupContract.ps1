$ErrorActionPreference = 'Stop'

$playerXaml = Get-Content -LiteralPath 'src/Eizo.App/Views/PlayerView.xaml' -Raw
$playerCode = Get-Content -LiteralPath 'src/Eizo.App/Views/PlayerView.xaml.cs' -Raw
$sourcesXaml = Get-Content -LiteralPath 'src/Eizo.App/Views/SourcesView.xaml' -Raw

foreach ($forbidden in @(
    'OpenMediaButton',
    'SubtitleQuickButton',
    'AudioQuickButton',
    'PlaybackInfoTitle',
    'VideoInfoText',
    'SourceInfoText'
)) {
    if ($playerXaml.Contains($forbidden) -or $playerCode.Contains($forbidden)) {
        throw "Player UI contract violation: obsolete control remains: $forbidden"
    }
}

foreach ($required in @(
    'PlaybackLoadingRing',
    'PlaybackLoadingMetricsText',
    'TracksPanel',
    'SubtitleTrackCombo',
    'AudioTrackCombo'
)) {
    if (-not $playerXaml.Contains($required)) {
        throw "Player UI contract violation: required control missing: $required"
    }
}

if ($playerXaml -notmatch 'AppTransientSurfaceBrush' -or
    $playerXaml -notmatch '<ProgressRing') {
    throw 'Player UI contract violation: loading surface must use app transient/Mica surface and native ProgressRing.'
}

if ($playerCode -notmatch 'SliderValueToRate' -or
    $playerCode -notmatch 'RateToSliderValue') {
    throw 'Player UI contract violation: playback-rate slider must use normalized 0.5x/1.0x/2.0 mapping.'
}

if ($playerCode -match 'FileOpenPicker' -or
    $playerCode -match 'Playback_OpenLocalMedia') {
    throw 'Player UI contract violation: player-local file picker entry was reintroduced.'
}

if ($sourcesXaml -notmatch 'SectionCardStyle') {
    throw 'Media-source UI contract violation: source entries must render as cards.'
}

Write-Host 'Player/source UI cleanup contract PASS.'
