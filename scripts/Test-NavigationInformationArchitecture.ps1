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

$home = Index-OrThrow $shell 'x:Name="HomeNav"'
$bangumi = Index-OrThrow $shell 'x:Name="BangumiNav"'
$calendar = Index-OrThrow $shell 'x:Name="CalendarNav"'
$seasonal = Index-OrThrow $shell 'x:Name="SeasonalNav"'
$discover = Index-OrThrow $shell 'x:Name="DiscoverNav"'
$following = Index-OrThrow $shell 'x:Name="FollowingNav"'
$library = Index-OrThrow $shell 'x:Name="CategoryNav"'
$anime = Index-OrThrow $shell 'x:Name="AnimeNav"'
$movies = Index-OrThrow $shell 'x:Name="MoviesNav"'
$series = Index-OrThrow $shell 'x:Name="SeriesNav"'
$sources = Index-OrThrow $shell 'x:Name="SourcesNav"'
$footer = Index-OrThrow $shell '<NavigationView.FooterMenuItems>'
$cache = Index-OrThrow $shell 'x:Name="CacheNav"'
$about = Index-OrThrow $shell 'x:Name="AboutNav"'
$settings = Index-OrThrow $shell 'x:Name="SettingsNav"'

if (-not ($home -lt $bangumi -and
          $bangumi -lt $calendar -and
          $calendar -lt $seasonal -and
          $seasonal -lt $discover -and
          $discover -lt $following -and
          $following -lt $library -and
          $library -lt $anime -and
          $anime -lt $movies -and
          $movies -lt $series -and
          $series -lt $sources -and
          $sources -lt $footer -and
          $footer -lt $cache -and
          $cache -lt $about -and
          $about -lt $settings)) {
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
