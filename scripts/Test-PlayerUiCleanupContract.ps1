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

if ($sourcesXaml -notmatch 'x:Name="CardSurface"' -or
    $sourcesXaml -notmatch '<ControlTemplate TargetType="ListViewItem">' -or
    $sourcesXaml -notmatch 'CornerRadius="10"' -or
    $sourcesXaml -notmatch 'PointerOver' -or
    $sourcesXaml -notmatch 'Pressed') {
    throw 'Media-source UI contract violation: the visible source-card surface must be rounded inside the ListViewItem control template.'
}

if ($sourcesXaml -notmatch 'HorizontalContentAlignment="Center"' -or
    $sourcesXaml -notmatch 'VerticalContentAlignment="Center"' -or
    $sourcesXaml -notmatch 'HorizontalAlignment="Center"') {
    throw 'Media-source UI contract violation: Scan now button content must stay centered in both idle and scanning states.'
}

Write-Host 'Player/source UI cleanup contract PASS.'
