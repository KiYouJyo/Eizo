param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot

function Read-Text([string] $relativePath) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Bangumi community contract missing file: $relativePath"
    }

    return [IO.File]::ReadAllText(
        $path,
        [Text.Encoding]::UTF8)
}

$client = Read-Text 'src/Eizo.Bangumi/BangumiCommunityClient.cs'
$repository = Read-Text 'src/Eizo.Bangumi/BangumiCommunityRepository.cs'
$parser = Read-Text 'src/Eizo.Bangumi/BangumiCommunityJsonParser.cs'
$models = Read-Text 'src/Eizo.Bangumi/BangumiModels.cs'
$view = Read-Text 'src/Eizo.App/Views/BangumiSubjectDetailView.xaml.cs'
$xaml = Read-Text 'src/Eizo.App/Views/BangumiSubjectDetailView.xaml'
$shell = Read-Text 'src/Eizo.App/MainWindow.xaml.cs'

foreach ($required in @(
    'https://next.bgm.tv/',
    'Eizo/0.4.5',
    'p1/subjects/{subjectId}/{resource}',
    '"comments"',
    '"reviews"',
    '"topics"',
    '"recs"',
    '"relations"',
    'AuthenticationHeaderValue')) {
    if (-not $client.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi private community client contract missing: $required"
    }
}

foreach ($required in @(
    'GetSubjectCommentsAsync',
    'GetSubjectReviewsAsync',
    'GetSubjectTopicsAsync',
    'GetSubjectRecommendationsAsync',
    'GetSubjectRelationsAsync')) {
    if (-not $repository.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi community repository contract missing: $required"
    }
}

foreach ($required in @(
    'ParseComments',
    'ParseReviews',
    'ParseTopics',
    'ParseRecommendations',
    'ParseRelations',
    'BangumiCommunityPage')) {
    $haystack = $parser + $models
    if (-not $haystack.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi community parser/model contract missing: $required"
    }
}

foreach ($required in @(
    'BangumiCommunityRepository.Default',
    'GetAccessTokenForRequest',
    'LoadCommunityAsync',
    'CommentsLoadMoreButton_Click',
    'ReviewsLoadMoreButton_Click',
    'TopicsLoadMoreButton_Click',
    'SubjectRequested?.Invoke',
    'https://bgm.tv/blog/',
    'https://bgm.tv/subject/topic/')) {
    if (-not $view.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi subject community UI contract missing: $required"
    }
}

foreach ($required in @(
    'CommentsTab',
    'ReviewsTab',
    'TopicsTab',
    'RelatedTab',
    'RecommendationsList',
    'RelationsList')) {
    if (-not $xaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi subject community XAML contract missing: $required"
    }
}

if (-not $shell.Contains(
        'view.SubjectRequested',
        [StringComparison]::Ordinal)) {
    throw 'Bangumi related-subject in-app navigation contract missing.'
}

Write-Host 'Eizo v0.4.5 Bangumi community integration contract PASS.'
