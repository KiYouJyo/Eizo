param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$catalogXaml = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Views/CatalogView.xaml') -Raw
$catalogCode = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Views/CatalogView.xaml.cs') -Raw
$detailXaml = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Views/DetailView.xaml') -Raw
$detailCode = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Views/DetailView.xaml.cs') -Raw
$aggregation = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Eizo.App/Models/CatalogSubjectModel.cs') -Raw

foreach ($required in @(
    '<GridView x:Name="ResultsList"',
    '<ItemsWrapGrid',
    'ItemWidth="204"',
    'ItemHeight="408"',
    '<Setter Property="Width" Value="192" />',
    '<Setter Property="Height" Value="396" />',
    '<Grid RowDefinitions="272,124">',
    'Source="{Binding Artwork}"',
    'Text="{Binding MetaLine}"',
    'Text="{Binding SourceLabel}"')) {
    if ($catalogXaml -notmatch [regex]::Escape($required)) {
        throw "Library visual-card contract missing from CatalogView.xaml: $required"
    }
}

if ($catalogXaml -match '<Border\s+Width="180"\s+Height="306"') {
    throw 'Library card must not reintroduce an inner fixed-size border smaller than the GridViewItem highlight bounds.'
}

if ($catalogXaml -match '<Border[^>]+Margin="4,4,4,8"') {
    throw 'Library card must not reintroduce the old inner margin that desynchronizes pointer highlight and card bounds.'
}

foreach ($required in @(
    'CatalogSubjectAggregator.Build(snapshot)',
    'CreateArtwork(metadata?.PosterUrl',
    'CreateArtwork(item.Metadata?.PosterUrl',
    'MediaLocationKind.RemoteUri',
    'MediaLocationKind.LocalFile',
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
    'ApplyPoster(metadata?.PosterUrl)',
    'ApplyBackdrop(metadata?.BackdropUrl)',
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

Write-Host 'Eizo v0.4.1 library aggregation and ordering UI contract PASS.'
