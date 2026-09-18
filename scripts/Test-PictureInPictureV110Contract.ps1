param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$expectedVersion = '1.1.0'
$expectedPackageVersion = '1.1.0.0'

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required PiP contract file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

function Assert-Contains([string]$text, [string]$needle, [string]$label) {
    if (-not $text.Contains($needle, [StringComparison]::Ordinal)) {
        throw "PiP contract missing: $label"
    }
}

$xaml = Read-Text 'src/Eizo.App/Views/PlayerView.xaml'
$player = Read-Text 'src/Eizo.App/Views/PlayerView.PictureInPicture.cs'
$window = Read-Text 'src/Eizo.App/MainWindow.PictureInPicture.cs'
$project = Read-Text 'src/Eizo.App/Eizo.App.csproj'
$manifest = Read-Text 'src/Eizo.App/Package.appxmanifest'

Assert-Contains $project '<Version>1.1.0</Version>' 'product version'
Assert-Contains $manifest 'Version="1.1.0.0"' 'package version'

Assert-Contains $xaml 'x:Name="PictureInPictureButton"' 'player-header PiP button'
Assert-Contains $xaml 'Click="PictureInPictureButton_Click"' 'PiP button click handler'
Assert-Contains $xaml 'Glyph="&#xE93A;"' 'Segoe Fluent MiniExpand PiP icon'
Assert-Contains $xaml '<playback:PlaybackView x:Name="PlaybackSurface"' 'existing PlaybackView surface'

Assert-Contains $window 'CompactOverlayPresenter.Create()' 'native CompactOverlay presenter'
Assert-Contains $window 'presenter.InitialSize = CompactOverlaySize.Medium;' 'native compact initial size'
Assert-Contains $window 'AppWindow.SetPresenter(presenter);' 'CompactOverlay activation'
Assert-Contains $window 'AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);' 'overlapped restoration'
Assert-Contains $window 'AppWindow.MoveAndResize(' 'window geometry restoration'
Assert-Contains $window 'AppWindow.Changed += PictureInPicture_AppWindowChanged;' 'external presenter synchronization'

Assert-Contains $player 'SetPictureInPictureVisualState(bool enabled)' 'PiP visual-state entry point'
Assert-Contains $player 'PlayerSplitView.IsPaneOpen = false;' 'sidebar collapse'
Assert-Contains $player 'LeftPlaybackControls.Visibility = Visibility.Collapsed;' 'compact control reduction'
Assert-Contains $player 'PreviousChapterButton.Visibility = Visibility.Collapsed;' 'chapter control reduction'
Assert-Contains $player 'NextChapterButton.Visibility = Visibility.Collapsed;' 'chapter control reduction'
Assert-Contains $player 'ShowPictureInPictureControls(restartAutoHide: true);' 'PiP control reveal/autohide'
Assert-Contains $player 'PictureInPictureControlsTimer_Tick' 'PiP auto-hide timer'
Assert-Contains $player 'VirtualKey.Escape' 'Escape exit'
Assert-Contains $player 'PointerWheelChangedEvent' 'PiP mouse-wheel volume hook'
Assert-Contains $player 'ControlsRow.Height = new GridLength(0d);' 'PiP removes reserved controls row'
Assert-Contains $player 'Grid.SetRowSpan(PlaybackSurfaceHost, 3);' 'PiP video fills the player frame'
Assert-Contains $player 'Grid.SetRowSpan(PlayerControlsPanel, 3);' 'PiP controls overlay the video'
Assert-Contains $player 'PlayerControlsPanel.VerticalAlignment = VerticalAlignment.Bottom;' 'PiP bottom overlay alignment'
Assert-Contains $player 'PictureInPictureControlsBackdrop.Visibility = Visibility.Visible;' 'PiP transparent control backdrop'
Assert-Contains $player 'ApplyPictureInPictureSubtitlePositions(double surfaceHeight)' 'PiP subtitle collision layout'
Assert-Contains $player 'PrimarySubtitleText.FontSize = Math.Clamp(surfaceHeight * 0.026d, 11d, 14d);' 'responsive primary subtitle size'
Assert-Contains $player 'SecondarySubtitleText.FontSize = Math.Clamp(surfaceHeight * 0.022d, 10d, 12d);' 'responsive secondary subtitle size'
Assert-Contains $player 'lowerBottom + secondaryHeight + gap' 'non-overlapping dual subtitle stack'
Assert-Contains $xaml 'x:Name="PictureInPictureControlsBackdrop"' 'PiP control gradient backdrop'
Assert-Contains $xaml 'Color="#00000000"' 'PiP transparent gradient start'
Assert-Contains $xaml 'Color="#CC000000"' 'PiP translucent gradient end'
Assert-Contains $player 'T(exit' 'localized PiP accessibility labels'
Assert-Contains $player '"Playback_ExitPictureInPicture"' 'localized PiP exit key'
Assert-Contains $player '"Playback_PictureInPicture"' 'localized PiP enter key'

if ($player.Contains('Exit picture in picture', [StringComparison]::Ordinal) -or
    $player.Contains('退出画中画', [StringComparison]::Ordinal) -or
    $player.Contains('ピクチャー イン ピクチャーを終了', [StringComparison]::Ordinal)) {
    throw 'Player PiP implementation must not contain hard-coded localized UI labels.'
}

foreach ($lang in @('zh-CN', 'ja-JP', 'en-US')) {
    $resw = Read-Text "src/Eizo.App/Strings/$lang/Resources.resw"
    Assert-Contains $resw 'name="Playback_PictureInPicture"' "$lang PiP label"
    Assert-Contains $resw 'name="Playback_ExitPictureInPicture"' "$lang PiP exit label"
}

Write-Host "Eizo $expectedVersion / $expectedPackageVersion picture-in-picture contract PASS."
