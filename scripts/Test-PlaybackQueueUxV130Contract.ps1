param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot

function Read-Text([string] $relativePath) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Eizo v1.3 playback UX contract missing file: $relativePath"
    }

    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$main = Read-Text 'src/Eizo.App/MainWindow.xaml.cs'
$player = Read-Text 'src/Eizo.App/Views/PlayerView.xaml'

foreach ($required in @(
    'ResolveCatalogSubjectForItem(item)',
    'CatalogSubjectAggregator.Build(',
    'MediaCatalogStore.Default',
    '.SnapshotForDisplay()',
    'subject.Items.Any(candidate =>',
    'SameCatalogLocation(')) {
    if (-not $main.Contains($required, [StringComparison]::Ordinal)) {
        throw "Home continue-watching queue reconstruction contract missing: $required"
    }
}

foreach ($required in @(
    'x:Key="PlaybackQueueListItemStyle"',
    'BasedOn="{StaticResource SourceListItemStyle}"',
    '<Setter Property="CornerRadius"',
    'Value="8"',
    'Value="0,2,12,2"',
    '<Setter Property="Template">',
    '<ControlTemplate TargetType="ListViewItem">',
    '<ListViewItemPresenter',
    'CornerRadius="{TemplateBinding CornerRadius}"',
    'SelectedBackground="{ThemeResource SubtleFillColorTertiaryBrush}"',
    'SelectedPointerOverBackground="{ThemeResource SubtleFillColorSecondaryBrush}"',
    'SelectedPressedBackground="{ThemeResource SubtleFillColorTertiaryBrush}"',
    'SelectionCheckMarkVisualEnabled="False"',
    'ItemContainerStyle="{StaticResource PlaybackQueueListItemStyle}"',
    'ScrollViewer.HorizontalScrollMode="Disabled"',
    'ScrollViewer.HorizontalScrollBarVisibility="Disabled"',
    'ScrollViewer.VerticalScrollBarVisibility="Auto"',
    'MinWidth="0"',
    'TextWrapping="NoWrap"',
    'TextTrimming="CharacterEllipsis"')) {
    if (-not $player.Contains($required, [StringComparison]::Ordinal)) {
        throw "Playback queue rounded selection contract missing: $required"
    }
}

Write-Host 'Eizo v1.3 home queue / playback queue highlight contract PASS.'
