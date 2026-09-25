param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot

function Read-Text([string] $relativePath) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Eizo v1.3 anime-index contract missing file: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

foreach ($removed in @(
    'src/Eizo.App/Views/BangumiAnimeBlogsView.xaml',
    'src/Eizo.App/Views/BangumiAnimeBlogsView.xaml.cs')) {
    if (Test-Path -LiteralPath (Join-Path $root $removed)) {
        throw "Removed Anime Blogs workspace still exists: $removed"
    }
}

$xaml = Read-Text 'src/Eizo.App/Views/BangumiAnimeIndexView.xaml'
$code = Read-Text 'src/Eizo.App/Views/BangumiAnimeIndexView.xaml.cs'
$shellXaml = Read-Text 'src/Eizo.App/MainWindow.xaml'
$shellCode = Read-Text 'src/Eizo.App/MainWindow.xaml.cs'
$models = Read-Text 'src/Eizo.Bangumi/BangumiModels.cs'
$client = Read-Text 'src/Eizo.Bangumi/BangumiApiClient.cs'
$repository = Read-Text 'src/Eizo.Bangumi/BangumiRepository.cs'

foreach ($required in @(
    'AutoSuggestBox x:Name="SearchBox"',
    'QueryIcon="Find"',
    'RadioButtons x:Name="FormatFilter"',
    'RadioButtons x:Name="SourceFilter"',
    'RadioButtons x:Name="GenreFilter"',
    'RadioButtons x:Name="RegionFilter"',
    'RadioButtons x:Name="AudienceFilter"',
    'RadioButtons x:Name="YearFilter"',
    'x:Name="FormatFilterLabel"',
    'x:Name="SourceFilterLabel"',
    'x:Name="GenreFilterLabel"',
    'x:Name="RegionFilterLabel"',
    'x:Name="AudienceFilterLabel"',
    'x:Name="YearFilterLabel"',
    'ComboBox x:Name="SortCombo"',
    'controls:MediaPosterCard',
    'PosterCardItemsPanelTemplate',
    'PosterCardGridViewItemStyle',
    'x:Name="PageScrollViewer"',
    'ViewChanged="PageScrollViewer_ViewChanged"',
    'x:Name="FilterExpander"',
    'IsExpanded="True"',
    'HorizontalAlignment="Right"')) {
    if (-not $xaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "Native anime-index XAML contract missing: $required"
    }
}

foreach ($forbidden in @(
    'WebView2',
    '<WebView',
    'BangumiAnimeBlogsView',
    'BlogsList',
    'LoadMoreButton',
    'Grid.Column="1"\r\n              CornerRadius="12"',
    'ResultsList_ContainerContentChanging',
    'ContainerContentChanging="ResultsList_ContainerContentChanging"')) {
    if ($xaml.Contains($forbidden, [StringComparison]::Ordinal) -or
        $code.Contains($forbidden, [StringComparison]::Ordinal) -or
        $shellCode.Contains($forbidden, [StringComparison]::Ordinal)) {
        throw "Anime index contains forbidden legacy/side-filter/manual-load content: $forbidden"
    }
}

foreach ($required in @(
    'private const int PageSize = 50;',
    'PageScrollViewer_ViewChanged',
    'const double preloadDistance = 520d;',
    'scrollViewer.ScrollableHeight -',
    'await LoadAsync(false, true);',
    '_hasMore = page.HasMore;',
    'BuildSearchQuery',
    'BangumiAnimeSearchQuery',
    'SearchAnimeAsync',
    'SubjectRequested?.Invoke',
    'IsDefaultRanking',
    'GetRankedAnimeAsync')) {
    if (-not $code.Contains($required, [StringComparison]::Ordinal)) {
        throw "Anime-index code-behind contract missing: $required"
    }
}

foreach ($required in @('BangumiAnimeSearchQuery','MetaTags','Tags','Year')) {
    if (-not $models.Contains($required, [StringComparison]::Ordinal)) {
        throw "Anime-index model contract missing: $required"
    }
}

foreach ($required in @(
    '"meta_tags"',
    '"tag"',
    '"air_date"',
    '"nsfw"',
    '"match" or "heat" or "rank" or "score"')) {
    if (-not $client.Contains($required, [StringComparison]::Ordinal)) {
        throw "Anime-index Bangumi query contract missing: $required"
    }
}

if (-not $repository.Contains('BangumiAnimeSearchQuery search',[StringComparison]::Ordinal)) {
    throw 'Anime-index repository search overload is missing.'
}

$searchIndex = $shellXaml.IndexOf('x:Name="AnimeIndexNav"', [StringComparison]::Ordinal)
$discoverIndex = $shellXaml.IndexOf('x:Name="DiscoverNav"', [StringComparison]::Ordinal)
$calendarIndex = $shellXaml.IndexOf('x:Name="CalendarNav"', [StringComparison]::Ordinal)
if ($searchIndex -lt 0 -or
    $discoverIndex -lt 0 -or
    $calendarIndex -lt 0 -or
    -not ($searchIndex -lt $discoverIndex -and $discoverIndex -lt $calendarIndex)) {
    throw 'Bangumi navigation must place Ranking & Discover directly below Anime Search.'
}
if (-not $shellXaml.Contains(
        '<NavigationViewItem.Icon><FontIcon Glyph="&#xE734;" /></NavigationViewItem.Icon>',
        [StringComparison]::Ordinal)) {
    throw 'Ranking & Discover must use its dedicated star/discovery icon.'
}

foreach ($required in @(
    'case "bangumi-anime-index":',
    'new BangumiAnimeIndexView()',
    'WireBangumiAnimeIndexView')) {
    if (-not $shellCode.Contains($required, [StringComparison]::Ordinal)) {
        throw "Anime-index shell routing contract missing: $required"
    }
}

foreach ($forbidden in @('bangumi-anime-blogs','AnimeBlogsNav','Nav_AnimeBlogs')) {
    if ($shellXaml.Contains($forbidden, [StringComparison]::Ordinal) -or
        $shellCode.Contains($forbidden, [StringComparison]::Ordinal)) {
        throw "Legacy Anime Blogs navigation remains: $forbidden"
    }
}

Write-Host 'Eizo v1.3.0 anime search top-filter / infinite-scroll contract PASS.'
