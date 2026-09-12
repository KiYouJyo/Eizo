$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$playerXaml = Get-Content -LiteralPath 'src/Eizo.App/Views/PlayerView.xaml' -Raw
$playerCode = Get-Content -LiteralPath 'src/Eizo.App/Views/PlayerView.xaml.cs' -Raw
$mainWindow = Get-Content -LiteralPath 'src/Eizo.App/MainWindow.xaml.cs' -Raw
$detailView = Get-Content -LiteralPath 'src/Eizo.App/Views/DetailView.xaml.cs' -Raw
$episodeGrouping = Get-Content -LiteralPath 'src/Eizo.App/Models/CatalogEpisodeGroupingResolver.cs' -Raw
$catalogSubject = Get-Content -LiteralPath 'src/Eizo.App/Models/CatalogSubjectModel.cs' -Raw
$selectionTrace = Get-Content -LiteralPath 'src/Eizo.App/Models/PlaybackSelectionTrace.cs' -Raw
$subtitleService = Get-Content -LiteralPath 'src/Eizo.App/Playback/ExternalSubtitleService.cs' -Raw
$subtitleMatcher = Get-Content -LiteralPath 'src/Eizo.App/Playback/ExternalSubtitleNameMatcher.cs' -Raw
$subtitleParser = Get-Content -LiteralPath 'src/Eizo.App/Playback/SubtitleTextParser.cs' -Raw
$queueModel = Get-Content -LiteralPath 'src/Eizo.App/Models/PlaybackQueueItemModel.cs' -Raw
$appSettings = Get-Content -LiteralPath 'src/Eizo.App/AppSettingsStore.cs' -Raw

foreach ($required in @(
    'SelectionChanged="QueueList_SelectionChanged"',
    'x:Name="PrimarySubtitleOverlay"',
    'HorizontalAlignment="Stretch"',
    'x:Name="PrimarySubtitlePositionSlider"',
    'ValueChanged="PrimarySubtitlePositionSlider_ValueChanged"',
    'x:Name="SecondarySubtitlePositionSlider"',
    'ValueChanged="SecondarySubtitlePositionSlider_ValueChanged"',
    'x:Name="PrimarySubtitleText"',
    'x:Name="SecondarySubtitleOverlay"',
    'x:Name="SecondarySubtitleCombo"',
    'SelectionChanged="SecondarySubtitleCombo_SelectionChanged"')) {
    if ($playerXaml -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 player XAML contract missing: $required"
    }
}

foreach ($required in @(
    'SwitchQueueItemAsync',
    'RefreshQueueStatus',
    'DiscoverAndAttachExternalSubtitlesAsync',
    'ExternalSubtitleService.DiscoverAsync',
    'UpdatePrimarySubtitle',
    'UpdateSecondarySubtitle',
    'FormatExternalSubtitleCandidate',
    'PlaybackState.Ended',
    '_queueIndex + 1',
    'Playback_PreviousEpisode',
    'Playback_NextEpisode')) {
    if ($playerCode -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 player code contract missing: $required"
    }
}

foreach ($required in @(
    'BuildPlaybackQueue',
    'CatalogSubjectModel? subject',
    'subject.Episodes',
    'new PlaybackQueueItemModel',
    'TryCreatePlaybackSource')) {
    if ($mainWindow -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 aggregated queue contract missing: $required"
    }
}

foreach ($required in @(
    'SeasonComboBox.SelectedItem is SeasonOption option',
    '(episode.SeasonNumber ?? 1) == selectedSeason',
    '.Select(static episode => episode.PrimaryItem)',
    'item ??= _subject.FirstPlayableItem')) {
    if ($detailView -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 selected-season playback contract missing: $required"
    }
}

foreach ($required in @(
    'ExplicitSpecialRegex',
    'OVA|OAD|ONA|SP|SPECIALS?',
    'explicitSpecial',
    'resolvedSeason = explicitSpecial',
    'IsSpecial: true')) {
    if ($episodeGrouping -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 stale-special grouping contract missing: $required"
    }
}

foreach ($required in @(
    'IsExplicitSpecialSource',
    'isSpecialGroup',
    'ResolveExplicitSpecialLabel',
    'Metadata currently carries EpisodeNumber/EpisodeTitle but no',
    'nativeTitle = string.Empty')) {
    if ($catalogSubject -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 special primary/title isolation contract missing: $required"
    }
}

foreach ($required in @(
    'playback-selection.log',
    'queueIndex',
    'queueCount')) {
    if ($selectionTrace -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 playback source trace contract missing: $required"
    }
}

foreach ($required in @(
    'DiscoverLocal',
    'MaterializeRemoteAsync',
    'DownloadFileAsync',
    'SubtitleCache',
    'external-subtitles.log',
    'ExternalSubtitleNameMatcher.IsMatch')) {
    if ($subtitleService -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 external subtitle service contract missing: $required"
    }
}

foreach ($required in @(
    '".srt"',
    '".vtt"',
    '".ass"',
    '".ssa"',
    'NormalizationForm.FormKC',
    '\u200B',
    '\uFEFF')) {
    if ($subtitleMatcher -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 external subtitle matcher contract missing: $required"
    }
}

foreach ($required in @(
    'ParseTimedText',
    'ParseAss',
    'GetText(TimeSpan position)',
    'CleanupAssText')) {
    if ($subtitleParser -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 secondary subtitle parser contract missing: $required"
    }
}

foreach ($required in @(
    '_primarySubtitleDocument',
    '_primarySubtitleUri',
    'ExternalSubtitleCandidate candidate',
    'ExternalSubtitleService.LoadDocumentAsync',
    'candidate.Uri != _primarySubtitleUri',
    'SelectSubtitleTrackAsync')) {
    if ($playerCode -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 WinUI primary subtitle contract missing: $required"
    }
}

if ($playerCode -match [regex]::Escape('AddExternalSubtitleAsync')) {
    throw 'v0.4.0 external primary subtitles must not be rendered by LibVLC.'
}

foreach ($required in @(
    'InitializeSubtitlePositionControls',
    'ApplySubtitlePositions',
    'PrimarySubtitlePositionSlider_ValueChanged',
    'SecondarySubtitlePositionSlider_ValueChanged',
    '_primarySubtitleVerticalPosition',
    '_secondarySubtitleVerticalPosition',
    'surfaceHeight *',
    'Math.Clamp(percentage, 0d, 90d)')) {
    if ($playerCode -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 independent subtitle position contract missing: $required"
    }
}

foreach ($required in @(
    'PrimarySubtitleVerticalPosition = 12d',
    'SecondarySubtitleVerticalPosition = 24d')) {
    if ($appSettings -notmatch [regex]::Escape($required)) {
        throw "v0.4.0 subtitle position persistence contract missing: $required"
    }
}

if ($queueModel -notmatch 'class PlaybackQueueItemModel' -or
    $queueModel -notmatch 'PlaybackSource Source' -or
    $queueModel -notmatch 'CatalogMediaItemModel\? CatalogItem') {
    throw 'v0.4.0 playback queue model contract is incomplete.'
}

Write-Host 'Eizo v0.4.0 playback queue / external subtitle / dual subtitle contract PASS.'
