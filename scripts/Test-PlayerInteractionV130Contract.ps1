param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot

function Read-Text([string] $relativePath) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Player interaction contract missing file: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$xaml = Read-Text 'src/Eizo.App/Views/PlayerView.xaml'
$code = Read-Text 'src/Eizo.App/Views/PlayerView.xaml.cs'

foreach ($required in @(
    'x:Name="DirectionSpeedBanner"',
    'HorizontalAlignment="Center"',
    'VerticalAlignment="Top"',
    'Background="#99000000"',
    'IsHitTestVisible="False"',
    'x:Name="DirectionSpeedBannerText"',
    'x:Name="VolumeBanner"',
    'x:Name="VolumeBannerText"',
    'Glyph="&#xE767;"')) {
    if (-not $xaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "Fullscreen speed banner contract missing: $required"
    }
}

foreach ($required in @(
    'PlaybackSurfaceHost.AddHandler(',
    'UIElement.TappedEvent',
    'new TappedEventHandler(PlaybackSurfaceHost_Tapped)',
    'PlaybackSurfaceHost_Tapped(',
    'await TogglePlayPauseAsync()')) {
    if (-not $code.Contains($required, [StringComparison]::Ordinal)) {
        throw "Video-surface click pause/play contract missing: $required"
    }
}

$availability = [regex]::Match(
    $code,
    'private void UpdateControlAvailability\(\)[\s\S]*?private void FullscreenButton_Click',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $availability.Success) {
    throw 'Player subtitle control-availability region not found.'
}
foreach ($required in @(
    'var primaryExternalOverlayActive =',
    'var primaryNativeSubtitleActive =',
    'PrimarySubtitlePositionSlider.IsEnabled =',
    'primaryExternalOverlayActive;',
    'PrimarySubtitleOpacitySlider.IsEnabled =',
    'PrimarySubtitleNativeHint.Visibility =',
    'primaryNativeSubtitleActive')) {
    if (-not $availability.Value.Contains($required, [StringComparison]::Ordinal)) {
        throw "Embedded subtitle style-lock contract missing: $required"
    }
}

$rebuild = [regex]::Match(
    $code,
    'private void RebuildSubtitleCombo\(IPlaybackTrackController tracks\)[\s\S]*?private void RebuildSecondarySubtitleCombo',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $rebuild.Success) {
    throw 'Primary subtitle selector region not found.'
}
foreach ($required in @(
    'var hasInternalSubtitles =',
    'tracks.SubtitleTracks.Count > 0',
    'if (!hasInternalSubtitles)',
    'foreach (var candidate in _externalSubtitles)')) {
    if (-not $rebuild.Value.Contains($required, [StringComparison]::Ordinal)) {
        throw "Embedded-primary subtitle contract missing: $required"
    }
}

$discover = [regex]::Match(
    $code,
    'private async Task DiscoverAndAttachExternalSubtitlesAsync[\s\S]*?private static async Task TryRestorePositionAsync',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $discover.Success) {
    throw 'Subtitle discovery region not found.'
}
foreach ($required in @(
    'engine.Tracks.SubtitleTracks.Count > 0',
    'if (!hasInternalSubtitles &&',
    'remembered is { Kind: "external" }',
    'automaticSecondaryCandidate')) {
    if (-not $discover.Value.Contains($required, [StringComparison]::Ordinal)) {
        throw "Embedded/external subtitle routing contract missing: $required"
    }
}

$keyRegion = [regex]::Match(
    $code,
    'private void PlayerView_KeyDown[\s\S]*?private void FullscreenControlsTimer_Tick',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $keyRegion.Success) {
    throw 'Fullscreen direction-key region not found.'
}
foreach ($required in @(
    'ShowDirectionSpeedBanner(temporaryRate)',
    'HideDirectionSpeedBanner()',
    'if (wasHold)',
    'VirtualKey.Up or VirtualKey.Down',
    'e.Key == VirtualKey.Up ? 0.05d : -0.05d',
    'ShowVolumeBanner(_volume)',
    'ShowFullscreenControls(restartAutoHide: true)')) {
    if (-not $keyRegion.Value.Contains($required, [StringComparison]::Ordinal)) {
        throw "Direction-hold interaction contract missing: $required"
    }
}
$hold = [regex]::Match(
    $code,
    'private void DirectionHoldTimer_Tick[\s\S]*?private void ShowDirectionSpeedBanner',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $hold.Success) {
    throw 'Direction hold handler not found.'
}
if ($hold.Value.Contains('ShowFullscreenControls(', [StringComparison]::Ordinal)) {
    throw 'Long-press direction speed must not summon fullscreen bottom controls.'
}

$wheel = [regex]::Match(
    $code,
    'private void PlayerRoot_PointerWheelChanged[\s\S]*?private void PlayerView_Loaded',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $wheel.Success) {
    throw 'Fullscreen wheel-volume handler not found.'
}
foreach ($required in @(
    'SetVolume(_volume + (Math.Sign(delta) * 0.05d))',
    'ShowVolumeBanner(_volume)')) {
    if (-not $wheel.Value.Contains($required, [StringComparison]::Ordinal)) {
        throw "Fullscreen wheel-volume banner contract missing: $required"
    }
}
if ($wheel.Value.Contains('ShowFullscreenControls(', [StringComparison]::Ordinal)) {
    throw 'Mouse-wheel volume changes must not summon fullscreen bottom controls.'
}

$volumeBanner = [regex]::Match(
    $code,
    'private void ShowVolumeBanner[\s\S]*?private void CancelDirectionKeyGesture',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $volumeBanner.Success -or
    -not $volumeBanner.Value.Contains('_volumeBannerTimer.Start()', [StringComparison]::Ordinal) -or
    -not $volumeBanner.Value.Contains('VolumeBanner.Visibility = Visibility.Visible', [StringComparison]::Ordinal)) {
    throw 'Transient top-center volume banner contract missing.'
}

Write-Host 'Eizo 1.3 player subtitle / fullscreen speed / volume / surface-click contract PASS.'
