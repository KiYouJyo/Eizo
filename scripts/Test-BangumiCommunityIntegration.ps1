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
$reviewView = Read-Text 'src/Eizo.App/Views/BangumiReviewDetailView.xaml.cs'
$topicView = Read-Text 'src/Eizo.App/Views/BangumiTopicDetailView.xaml.cs'
$reviewXaml = Read-Text 'src/Eizo.App/Views/BangumiReviewDetailView.xaml'
$topicXaml = Read-Text 'src/Eizo.App/Views/BangumiTopicDetailView.xaml'
$communityText = Read-Text 'src/Eizo.App/Models/BangumiCommunityText.cs'
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
    'p1/blogs/{entryId}',
    'p1/blogs/{entryId}/comments',
    'p1/subjects/-/topics/{topicId}',
    'p1/channels/{type}/blogs',
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
    'GetSubjectRelationsAsync',
    'GetBlogEntryAsync',
    'GetBlogCommentsAsync',
    'GetSubjectTopicAsync',
    'GetChannelBlogsAsync',
    'LikeSubjectCommentAsync',
    'UnlikeSubjectCommentAsync',
    'LikeSubjectPostAsync',
    'UnlikeSubjectPostAsync')) {
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
    'ParseBlogEntry',
    'ParseBlogComments',
    'ParseTopicDetail',
    'ParseChannelBlogs',
    'BangumiChannelBlog',
    'BangumiCommunityPage',
    'BangumiBlogDetail',
    'BangumiTopicDetail')) {
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
    'CommentsFilterCombo_SelectionChanged',
    '_commentsFilter',
    'ReviewsLoadMoreButton_Click',
    'TopicsLoadMoreButton_Click',
    'SubjectRequested?.Invoke',
    'ReviewRequested?.Invoke',
    'TopicRequested?.Invoke',
    'CommentReactionButton_Click')) {
    if (-not $view.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi subject community UI contract missing: $required"
    }
}

foreach ($required in @(
    'ParseContentBlocks',
    'https://lain.bgm.tv/pic/photo/l/',
    'BarePhotoPathRegex',
    'PhotoTagRegex',
    'PhotoEqualsTagRegex',
    'HtmlImageTagRegex')) {
    if (-not $communityText.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi blog content rendering contract missing: $required"
    }
}

foreach ($required in @(
    'GetBlogEntryAsync',
    'GetBlogCommentsAsync',
    'BangumiCommunityText.ToPlainText',
    'https://bgm.tv/blog/',
    'RenderContent',
    'ParseContentBlocks')) {
    if (-not $reviewView.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi review detail contract missing: $required"
    }
}

foreach ($required in @(
    'GetSubjectTopicAsync',
    'BangumiCommunityText.ToPlainText',
    'https://bgm.tv/subject/topic/',
    'ReplyReactionButton_Click')) {
    if (-not $topicView.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi topic detail contract missing: $required"
    }
}


foreach ($required in @(
    'CommentsTab',
    'CommentsFilterCombo',
    'ReviewsTab',
    'TopicsTab',
    'RelatedTab',
    'RecommendationsList',
    'RelationsList')) {
    if (-not $xaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi subject community XAML contract missing: $required"
    }
}

foreach ($required in @(
    'view.SubjectRequested',
    'view.ReviewRequested',
    'view.TopicRequested',
    'OpenBangumiReview',
    'OpenBangumiTopic',
    'new BangumiReviewDetailView',
    'new BangumiTopicDetailView')) {
    if (-not $shell.Contains($required, [StringComparison]::Ordinal)) {
        throw "Bangumi in-app community navigation contract missing: $required"
    }
}

Write-Host 'Eizo v0.4.5 Bangumi community integration contract PASS.'
