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
$detail = Read-Text 'src/Eizo.App/Views/BangumiSubjectDetailView.xaml.cs'
$shell = Read-Text 'src/Eizo.App/MainWindow.xaml.cs'

foreach ($required in @(
    '../Eizo.Bangumi/Eizo.Bangumi.csproj',
    'https://api.bgm.tv/',
    'Eizo/0.4.2',
    'v0/subjects?type=2&cat=1&sort=date',
    'v0/subjects?type=2&sort=rank',
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
    'GetRankedAnimeAsync',
    'GetCalendarAsync',
    'GetSubjectAsync')) {
    if (-not $repository.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi repository contract missing: $required"
    }
}

foreach ($required in @(
    'ParsePagedSubjects',
    'ParseCalendar',
    'ParseSubject',
    'subject.Type == 2',
    'ResolvePoster')) {
    if (-not $parser.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi parser contract missing: $required"
    }
}

foreach ($required in @(
    'GetCalendarAsync',
    'GetCurrentSeasonAsync',
    'GetRankedAnimeAsync',
    'SubjectRequested?.Invoke',
    'Bangumi_StaleCacheFormat')) {
    if (-not $view.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi public view contract missing: $required"
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
    'new BangumiPublicView(BangumiPublicPageKind.Calendar)',
    'new BangumiPublicView(BangumiPublicPageKind.Seasonal)',
    'new BangumiPublicView(BangumiPublicPageKind.Discover)',
    'new BangumiPlaceholderView(BangumiPlaceholderKind.Following)')) {
    if (-not $shell.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi shell routing contract missing: $required"
    }
}

Write-Host 'Eizo v0.4.2 Bangumi public integration contract PASS.'
