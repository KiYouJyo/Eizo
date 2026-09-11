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
    'Source="{Binding Artwork}"',
    'Text="{Binding MetaLine}"',
    'Text="{Binding SourceLabel}"')) {
    if ($catalogXaml -notmatch [regex]::Escape($required)) {
        throw "Library visual-card contract missing from CatalogView.xaml: $required"
    }
}

foreach ($required in @(
    'CatalogSubjectAggregator.Build(snapshot)',
    'CreateArtwork(metadata?.PosterUrl',
    'CreateArtwork(item.Metadata?.PosterUrl',
    'MediaLocationKind.RemoteUri',
    'MediaLocationKind.LocalFile')) {
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

Write-Host 'Eizo v0.3.8 library aggregation visual UI contract PASS.'
