param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot

function Read-Text([string] $relativePath) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Bangumi integration contract missing file: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$project = Read-Text 'src/Eizo.App/Eizo.App.csproj'
$client = Read-Text 'src/Eizo.Bangumi/BangumiApiClient.cs'
$repository = Read-Text 'src/Eizo.Bangumi/BangumiRepository.cs'
$parser = Read-Text 'src/Eizo.Bangumi/BangumiJsonParser.cs'
$view = Read-Text 'src/Eizo.App/Views/BangumiPublicView.xaml.cs'
$viewXaml = Read-Text 'src/Eizo.App/Views/BangumiPublicView.xaml'
$detail = Read-Text 'src/Eizo.App/Views/BangumiSubjectDetailView.xaml.cs'
$homeView = Read-Text 'src/Eizo.App/Views/HomeView.xaml.cs'
$shell = Read-Text 'src/Eizo.App/MainWindow.xaml.cs'

foreach ($required in @(
    '../Eizo.Bangumi/Eizo.Bangumi.csproj',
    'https://api.bgm.tv/',
    'Eizo/0.5.11',
    '"type=2"',
    '"sort=" + sort',
    'GetSeasonMonthAsync',
    'GetRankedAnimeAsync',
    '"calendar"',
    'v0/subjects/{subjectId}')) {
    $haystack = $project + $client
    if (-not $haystack.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi public API contract missing: $required"
    }
}

foreach ($required in @(
    'SeasonCacheLifetime',
    'RankingCacheLifetime',
    'CalendarCacheLifetime',
    'SubjectCacheLifetime',
    'IsStale: true',
    'GetCurrentSeasonAsync',
    'GetSeasonAsync',
    'GetRankedAnimeAsync',
    'GetRankedAnimeForYearAsync',
    'GetRankedAnimeForSeasonAsync',
    'ShiftSeason',
    'Enumerable.Range(startMonth, 3)',
    'GetCalendarAsync',
    'GetSubjectAsync',
    'GetSubjectCreditsAsync')) {
    if (-not $repository.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi repository contract missing: $required"
    }
}

foreach ($required in @(
    'ParsePagedSubjects',
    'ParsePagedSubjectPage',
    'ParseCalendar',
    'ParseSubject',
    'ParseSubjectCharacters',
    'ParseSubjectPersons',
    'subject.Type == 2',
    'ResolvePoster')) {
    if (-not $parser.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi parser contract missing: $required"
    }
}

foreach ($required in @(
    'GetCalendarAsync',
    'GetSeasonAsync',
    'GetRankedAnimeAsync',
    'GetRankedAnimeForYearAsync',
    'GetRankedAnimeForSeasonAsync',
    'PreviousSeasonButton_Click',
    'NextSeasonButton_Click',
    'SeasonPickerButton_Click',
    'new ContentDialog',
    'XamlRoot = XamlRoot',
    'new ComboBox',
    'new RadioButtons',
    'SeasonPickerOption',
    'LoadMoreButton_Click',
    'SubjectRequested?.Invoke',
    'Bangumi_StaleCacheFormat')) {
    if (-not $view.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi public view contract missing: $required"
    }
}

foreach ($required in @(
    'x:Name="SeasonPickerButton"',
    'Click="SeasonPickerButton_Click"',
    'x:Name="SelectedSeasonText"')) {
    if (-not $viewXaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi seasonal picker XAML contract missing: $required"
    }
}

foreach ($required in @(
    'GetSubjectAsync',
    'https://bgm.tv/subject/',
    'Bangumi_OpenOnBangumi')) {
    if (-not $detail.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi subject detail contract missing: $required"
    }
}


foreach ($required in @(
    'GetCurrentSeasonAsync',
    'BangumiSubjectRequested',
    'BangumiSeasonalRequested',
    'CreateSeasonCard',
    'Take(8)')) {
    if (-not $homeView.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi home integration contract missing: $required"
    }
}

foreach ($required in @(
    'new BangumiPublicView(BangumiPublicPageKind.Calendar)',
    'new BangumiPublicView(BangumiPublicPageKind.Seasonal)',
    'new BangumiPublicView(BangumiPublicPageKind.Discover)',
    'new BangumiFollowingView()',
    'view.BangumiSubjectRequested',
    'view.BangumiSeasonalRequested')) {
    if (-not $shell.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi shell routing contract missing: $required"
    }
}

Write-Host 'Eizo v0.5.11 Bangumi public integration contract PASS.'
