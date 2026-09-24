param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$catalogXaml = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Views/CatalogView.xaml') -Raw
$catalogCode = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Views/CatalogView.xaml.cs') -Raw
$cardStyles = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Themes/CardStyles.xaml') -Raw
$posterCard = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Controls/MediaPosterCard.xaml') -Raw
$detailXaml = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Views/DetailView.xaml') -Raw
$detailCode = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Views/DetailView.xaml.cs') -Raw
$aggregation = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Models/CatalogSubjectModel.cs') -Raw
$presentation = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Models/CatalogSubjectPresentation.cs') -Raw

foreach ($required in @(
    '<GridView x:Name="ResultsList"',
    'ItemsPanel="{StaticResource PosterCardItemsPanelTemplate}"',
    'ItemContainerStyle="{StaticResource PosterCardGridViewItemStyle}"',
    '<controls:MediaPosterCard',
    'Artwork="{Binding Artwork}"',
    'MetaLine="{Binding MetaLine}"')) {
    if ($catalogXaml -notmatch [regex]::Escape($required)) {
        throw "Library shared-card contract missing from CatalogView.xaml: $required"
    }
}

foreach ($required in @(
    '<x:Double x:Key="EizoPosterCardItemWidth">204</x:Double>',
    '<x:Double x:Key="EizoPosterCardItemHeight">408</x:Double>',
    '<x:Double x:Key="EizoPosterCardWidth">192</x:Double>',
    '<x:Double x:Key="EizoPosterCardHeight">396</x:Double>',
    'x:Key="PosterCardGridViewItemStyle"',
    'x:Name="InteractionOverlay"',
    'CornerRadius="12"')) {
    if ($cardStyles -notmatch [regex]::Escape($required)) {
        throw "Shared poster-card style contract missing: $required"
    }
}

foreach ($required in @(
    '<Grid RowDefinitions="272,124">',
    'Source="{x:Bind Artwork, Mode=OneWay}"',
    'Text="{x:Bind MetaLine, Mode=OneWay}"')) {
    if ($posterCard -notmatch [regex]::Escape($required)) {
        throw "Reusable MediaPosterCard contract missing: $required"
    }
}

$cardSurface = $catalogXaml + $cardStyles + $posterCard
if ($cardSurface -match '<Border\s+Width="180"\s+Height="306"') {
    throw 'Library card must not reintroduce an inner fixed-size border smaller than the GridViewItem highlight bounds.'
}

if ($cardSurface -match '<Border[^>]+Margin="4,4,4,8"') {
    throw 'Library card must not reintroduce the old inner margin that desynchronizes pointer highlight and card bounds.'
}

foreach ($required in @(
    'CatalogSubjectAggregator.Build(snapshot)',
    'CatalogSubjectPresentation.Create(subject)',
    'CreateArtwork(',
    'presentation.PosterUrl',
    'CreateArtwork(item.Metadata?.PosterUrl',
    'private readonly MediaCategoryKind? _categoryFilter',
    'entry.Category == _categoryFilter',
    '.ThenBy(PreferredMetadataOrder)',
    'HasPreferredMetadata(CatalogMediaItemModel item)',
    '!string.IsNullOrWhiteSpace(metadata.PosterUrl)')) {
    if ($catalogCode -notmatch [regex]::Escape($required)) {
        throw "Library visual-card code contract missing: $required"
    }
}


foreach ($required in @(
    'CatalogSubjectPresentation(',
    'MediaLocationKind.RemoteUri',
    'MediaLocationKind.LocalFile',
    'SourceCount',
    'HasLocalSource',
    'HasRemoteSource')) {
    if ($presentation -notmatch [regex]::Escape($required)) {
        throw "Library presentation contract missing: $required"
    }
}

foreach ($required in @(
    'x:Name="BackdropImage"',
    'x:Name="PosterImage"',
    'x:Name="ReleaseStatText"',
    'x:Name="EpisodeStatText"',
    'x:Name="SourceStatText"',
    'x:Name="SeasonComboBox"',
    'x:Name="EpisodeList"',
    'Source="{x:Bind Thumbnail}"')) {
    if ($detailXaml -notmatch [regex]::Escape($required)) {
        throw "Aggregated detail UI contract missing: $required"
    }
}

foreach ($required in @(
    'CatalogSubjectPresentation.Create(_subject)',
    'ApplyPoster(presentation.PosterUrl)',
    'ApplyBackdrop(presentation.BackdropUrl)',
    'EpisodeThumbnailUrl',
    'RebuildEpisodeList()',
    'MediaPlayRequested?.Invoke')) {
    if ($detailCode -notmatch [regex]::Escape($required)) {
        throw "Aggregated detail code contract missing: $required"
    }
}

if ($aggregation -notmatch [regex]::Escape('AssignSubjectIdentities(groupingInputs)')) {
    throw 'Catalog aggregation is not using reconciled Metadata/Recognition subject identities.'
}

foreach ($required in @(
    'OrderByDescending(static metadata =>',
    '!string.IsNullOrWhiteSpace(metadata.PosterUrl)',
    'OrderByDescending(static item =>',
    '!string.IsNullOrWhiteSpace(value.PosterUrl)')) {
    if ($aggregation -notmatch [regex]::Escape($required)) {
        throw "Poster-backed aggregated metadata preference contract missing: $required"
    }
}

Write-Host 'Eizo v0.5.10 library aggregation and unified presentation UI contract PASS.'
