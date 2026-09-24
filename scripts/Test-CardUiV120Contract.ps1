param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required v1.2 card UI file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$app = Read-Text 'src/Eizo.App/App.xaml'
$styles = Read-Text 'src/Eizo.App/Themes/CardStyles.xaml'
$card = Read-Text 'src/Eizo.App/Controls/MediaPosterCard.xaml'
$cardCode = Read-Text 'src/Eizo.App/Controls/MediaPosterCard.xaml.cs'
$catalog = Read-Text 'src/Eizo.App/Views/CatalogView.xaml'
$following = Read-Text 'src/Eizo.App/Views/BangumiFollowingView.xaml'
$cache = Read-Text 'src/Eizo.App/Views/CacheView.xaml'
$cacheCode = Read-Text 'src/Eizo.App/Views/CacheView.xaml.cs'

if (-not $app.Contains(
        '<ResourceDictionary Source="Themes/CardStyles.xaml" />',
        [StringComparison]::Ordinal)) {
    throw 'The shared card resource dictionary is not loaded by App.xaml.'
}

foreach ($required in @(
    'EizoPosterCardWidth',
    'EizoPosterCardHeight',
    'PosterCardItemsPanelTemplate',
    'PosterCardGridViewItemStyle',
    'MediaPosterCardRootStyle',
    'CacheCardListItemStyle',
    'DangerMenuFlyoutItemStyle',
    'x:Name="InteractionOverlay"',
    'x:Name="FocusOverlay"')) {
    if (-not $styles.Contains($required, [StringComparison]::Ordinal)) {
        throw "Shared card style contract missing: $required"
    }
}

foreach ($view in @(
    @{ Name = 'Catalog'; Text = $catalog },
    @{ Name = 'Bangumi My Following'; Text = $following })) {
    foreach ($required in @(
        'ItemsPanel="{StaticResource PosterCardItemsPanelTemplate}"',
        'ItemContainerStyle="{StaticResource PosterCardGridViewItemStyle}"',
        '<controls:MediaPosterCard')) {
        if (-not $view.Text.Contains($required, [StringComparison]::Ordinal)) {
            throw "$($view.Name) is not using the shared poster-card contract: $required"
        }
    }

    if ($view.Text.Contains(
            '<ControlTemplate TargetType="GridViewItem">',
            [StringComparison]::Ordinal)) {
        throw "$($view.Name) still duplicates a GridViewItem interaction template."
    }
}

foreach ($required in @(
    '<Grid RowDefinitions="272,124">',
    'Style="{StaticResource MediaPosterCardRootStyle}"',
    'Style="{StaticResource MediaPosterArtworkStyle}"',
    'Text="{x:Bind Title, Mode=OneWay}"',
    'Text="{x:Bind Subtitle, Mode=OneWay}"',
    'Text="{x:Bind MetaLine, Mode=OneWay}"',
    'Text="{x:Bind FooterLine, Mode=OneWay}"')) {
    if (-not $card.Contains($required, [StringComparison]::Ordinal)) {
        throw "Reusable MediaPosterCard layout contract missing: $required"
    }
}

foreach ($required in @(
    'ArtworkProperty',
    'IconGlyphProperty',
    'BadgeTextProperty',
    'TitleProperty',
    'SubtitleProperty',
    'MetaLineProperty',
    'FooterLineProperty',
    'UpdateFooterVisibility')) {
    if (-not $cardCode.Contains($required, [StringComparison]::Ordinal)) {
        throw "Reusable MediaPosterCard binding contract missing: $required"
    }
}

if ($catalog.Contains(
        'FooterLine="{Binding SourceLabel}"',
        [StringComparison]::Ordinal)) {
    throw 'Library cards must keep source diagnostics hidden in the standard presentation.'
}

if (-not $following.Contains(
        'FooterLine="{Binding SourceLabel}"',
        [StringComparison]::Ordinal)) {
    throw 'My Following cards must retain their collection progress/footer line.'
}

foreach ($required in @(
    'ItemContainerStyle="{StaticResource CacheCardListItemStyle}"',
    'ContainerContentChanging="CacheList_ContainerContentChanging"')) {
    if (-not $cache.Contains($required, [StringComparison]::Ordinal)) {
        throw "Cache card shared-style contract missing: $required"
    }
}

foreach ($required in @(
    'CacheList_ContainerContentChanging',
    'OpenCacheFolder_Click',
    'ResolveCacheFolderPath',
    'CacheRuntime.Store.RootPath',
    'DeleteCacheItem_Click',
    'DangerMenuFlyoutItemStyle')) {
    if (-not $cacheCode.Contains($required, [StringComparison]::Ordinal)) {
        throw "Cache context-action contract missing: $required"
    }
}

if ($cacheCode.Contains(
        'ContentDialog',
        [StringComparison]::Ordinal)) {
    throw 'Cache deletion must remain immediate and must not add a confirmation dialog.'
}

Write-Host 'Eizo v1.2.0 unified card UI contract PASS.'
