$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$shellXamlPath = Join-Path $root 'src/Eizo.App/MainWindow.xaml'
$shellCodePath = Join-Path $root 'src/Eizo.App/MainWindow.xaml.cs'

$shell = Get-Content -Raw -LiteralPath $shellXamlPath
$code = Get-Content -Raw -LiteralPath $shellCodePath

function Index-OrThrow([string] $text, [string] $needle) {
    $index = $text.IndexOf($needle, [StringComparison]::Ordinal)
    if ($index -lt 0) {
        throw "Navigation IA contract violation: missing '$needle'."
    }

    return $index
}

$homeIndex = Index-OrThrow $shell 'x:Name="HomeNav"'
$bangumiIndex = Index-OrThrow $shell 'x:Name="BangumiNav"'
$calendarIndex = Index-OrThrow $shell 'x:Name="CalendarNav"'
$seasonalIndex = Index-OrThrow $shell 'x:Name="SeasonalNav"'
$discoverIndex = Index-OrThrow $shell 'x:Name="DiscoverNav"'
$followingIndex = Index-OrThrow $shell 'x:Name="FollowingNav"'
$libraryIndex = Index-OrThrow $shell 'x:Name="CategoryNav"'
$animeIndex = Index-OrThrow $shell 'x:Name="AnimeNav"'
$moviesIndex = Index-OrThrow $shell 'x:Name="MoviesNav"'
$seriesIndex = Index-OrThrow $shell 'x:Name="SeriesNav"'
$sourcesIndex = Index-OrThrow $shell 'x:Name="SourcesNav"'
$footerIndex = Index-OrThrow $shell '<NavigationView.FooterMenuItems>'
$cacheIndex = Index-OrThrow $shell 'x:Name="CacheNav"'
$aboutIndex = Index-OrThrow $shell 'x:Name="AboutNav"'
$settingsIndex = Index-OrThrow $shell 'x:Name="SettingsNav"'

if (-not ($homeIndex -lt $bangumiIndex -and
          $bangumiIndex -lt $calendarIndex -and
          $calendarIndex -lt $seasonalIndex -and
          $seasonalIndex -lt $discoverIndex -and
          $discoverIndex -lt $followingIndex -and
          $followingIndex -lt $libraryIndex -and
          $libraryIndex -lt $animeIndex -and
          $animeIndex -lt $moviesIndex -and
          $moviesIndex -lt $seriesIndex -and
          $seriesIndex -lt $sourcesIndex -and
          $sourcesIndex -lt $footerIndex -and
          $footerIndex -lt $cacheIndex -and
          $cacheIndex -lt $aboutIndex -and
          $aboutIndex -lt $settingsIndex)) {
    throw 'Navigation IA contract violation: hamburger-menu ordering changed.'
}

if ($shell -notmatch 'x:Name="BangumiNav"[sS]*?SelectsOnInvoked="False"[sS]*?IsExpanded="True"') {
    throw 'Navigation IA contract violation: Bangumi must remain an expandable non-workspace group.'
}

if ($shell -notmatch 'x:Name="CategoryNav"[sS]*?Tag="categories"[sS]*?IsExpanded="True"') {
    throw 'Navigation IA contract violation: media-library parent must retain the existing aggregate catalog workspace.'
}

$requiredMappings = @(
    'case "home":',
    'new HomeView()',
    'case "categories":',
    'new CatalogView()',
    'new CategoryView(MediaCategoryKind.Anime)',
    'new CategoryView(MediaCategoryKind.Movies)',
    'new CategoryView(MediaCategoryKind.Series)',
    'case "sources":',
    'return new SourcesView();',
    'case "cache":',
    'return new CacheView();',
    'case "about":',
    'return new AboutView();',
    'case "settings":',
    'return new SettingsView();',
    'case "bangumi-calendar":',
    'case "bangumi-seasonal":',
    'case "bangumi-discover":',
    'case "bangumi-following":',
    'new BangumiPlaceholderView(BangumiPlaceholderKind.Calendar)',
    'new BangumiPlaceholderView(BangumiPlaceholderKind.Seasonal)',
    'new BangumiPlaceholderView(BangumiPlaceholderKind.Discover)',
    'new BangumiPlaceholderView(BangumiPlaceholderKind.Following)'
)

foreach ($mapping in $requiredMappings) {
    if (-not $code.Contains($mapping, [StringComparison]::Ordinal)) {
        throw "Navigation IA contract violation: workspace mapping '$mapping' is missing."
    }
}

Write-Host 'Navigation information architecture contract PASS.'
