$ErrorActionPreference = 'Stop'

$sourcesView = Get-Content -LiteralPath 'src/Eizo.App/Views/SourcesView.xaml.cs' -Raw
$sourcesXaml = Get-Content -LiteralPath 'src/Eizo.App/Views/SourcesView.xaml' -Raw
$sourceStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaSourceStore.cs' -Raw
$credentialStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaCredentialStore.cs' -Raw
$catalogStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaCatalogStore.cs' -Raw
$scanCoordinator = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaScanCoordinator.cs' -Raw
$scanModels = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaScanModels.cs' -Raw
$folderPicker = Get-Content -LiteralPath 'src/Eizo.App/Views/WebDavFolderPickerWindow.cs' -Raw

foreach ($required in @(
    'ShowWebDavEditorAsync',
    'CommitWebDavEditorAsync',
    'EditWebDavFoldersMenuItem_Click',
    'RollBackWebDavTarget',
    'InlineWebDavCredentialProvider',
    'Sources_EditWebDavTitle')) {
    if ($sourcesView -notmatch [regex]::Escape($required)) {
        throw "v0.3.1 WebDAV source-management contract missing: $required"
    }
}

if ($sourcesView -notmatch 'WebDavFolderPickerWindow' -or
    $sourcesView -notmatch 'EditWebDavFoldersMenuItem_Click' -or
    $sourcesView -notmatch 'SourceList_ItemClick' -or
    $sourcesView -notmatch 'Sources_EditReadFolders' -or
    $folderPicker -notmatch '_provider\.ListAsync\(') {
    throw 'v0.3.1 contract violation: source editing and folder editing must be separate WebDAV actions.'
}

if ($sourcesXaml -match 'x:Name="SectionList"' -or
    $sourcesView -match 'SectionList\.') {
    throw 'v0.3.1 contract violation: the obsolete Sources section selector must be removed.'
}

foreach ($cardRequirement in @(
    'IsItemClickEnabled="True"',
    'ItemClick="SourceList_ItemClick"',
    'ScanSourceButton_Click',
    '<ProgressRing',
    'CardBackgroundFillColorDefaultBrush',
    'CornerRadius')) {
    if ($sourcesXaml -notmatch [regex]::Escape($cardRequirement)) {
        throw "v0.3.1 source-card contract missing: $cardRequirement"
    }
}

if ($sourcesView -match 'scan\.Click \+= ScanSourceMenuItem_Click' -or
    $sourcesView -match 'editSource\.Click \+= EditWebDavMenuItem_Click') {
    throw 'v0.3.1 contract violation: scan/source edit must not be hidden in the source context menu.'
}

if ($sourcesView -match 'TestConnectionMenuItem_Click' -or
    $sourcesView -match 'Text = T\("Sources_TestConnection"\)') {
    throw 'v0.3.1 contract violation: the standalone Test connection source action must be removed.'
}

if ($sourcesView -notmatch '!source\.IsBuiltIn') {
    throw 'v0.3.1 contract violation: the built-in opened-local-videos pseudo source must stay hidden from the Sources page.'
}

if ($sourcesView -notmatch 'RequestedTheme = dialogTheme' -or
    $sourcesView -notmatch 'ResolveOverlayTheme\(') {
    throw 'v0.3.1 contract violation: source dialogs must explicitly follow the live light/dark page theme.'
}

if ($folderPicker -match 'Application\.Current\.Resources\[\s*"TextFillColorSecondaryBrush"\s*\]') {
    throw 'v0.3.1 contract violation: folder-picker text must not resolve secondary color from stale app-level theme resources.'
}

if ($sourcesView -notmatch 'MediaScanCoordinator\.Default' -or
    $sourcesView -notmatch '_scanCoordinator\.Changed' -or
    $sourcesView -notmatch '_scanCoordinator\.StartAsync\(' -or
    $sourcesView -notmatch 'Sources_Scanning' -or
    $catalogStore -notmatch 'Action<MediaScanProgress>' -or
    $scanCoordinator -notmatch 'MediaScanStatus\.Running' -or
    $scanModels -notmatch 'VideosDiscovered') {
    throw 'v0.3.1 contract violation: manual scan must expose application-level asynchronous progress that survives page navigation.'
}

if ($sourcesView -notmatch 'ScanProgressSize: isScanning \? 16 : 0' -or
    $sourcesView -notmatch 'ScanSpacing: isScanning \? 8 : 0' -or
    $sourcesXaml -notmatch 'MinWidth="0"' -or
    $sourcesXaml -notmatch 'ScanProgressSize') {
    throw 'v0.3.1 contract violation: idle scan button must stay compact and only expand when the ProgressRing is active.'
}

$editorStart = $sourcesView.IndexOf('private async Task ShowWebDavEditorAsync')
$editorEnd = $sourcesView.IndexOf('private async Task CommitWebDavEditorAsync', $editorStart)
if ($editorStart -lt 0 -or $editorEnd -le $editorStart) {
    throw 'v0.3.1 contract violation: WebDAV source editor block could not be resolved.'
}

$editorBlock = $sourcesView.Substring($editorStart, $editorEnd - $editorStart)
foreach ($forbidden in @(
    'WebDavFolderPickerWindow',
    'SelectedPaths',
    'Common_Browse',
    'folderSelectionSummary',
    'BrowseWebDavFoldersAsync')) {
    if ($editorBlock -match [regex]::Escape($forbidden)) {
        throw "v0.3.1 contract violation: media-source editor still owns folder-selection UI: $forbidden"
    }
}

if ($sourcesView -match 'BrowseWebDavFoldersAsync') {
    throw 'v0.3.1 contract violation: the legacy editor-to-folder-picker bridge must be removed.'
}

foreach ($pickerRequirement in @(
    'BreadcrumbBar',
    'CheckBox',
    'ScrollViewer.SetHorizontalScrollMode',
    'ScrollMode.Disabled',
    'ScrollViewer.SetHorizontalScrollBarVisibility',
    'ScrollBarVisibility.Disabled',
    'Sources_SelectedFoldersFormat',
    'Sources_SelectCurrentFolder',
    'DoubleTapped',
    'new SizeInt32(',
    '1040',
    '840',
    'titleBar.BackgroundColor',
    'titleBar.ButtonBackgroundColor',
    'RequestedTheme = _windowTheme',
    'Background =')) {
    if ($folderPicker -notmatch [regex]::Escape($pickerRequirement)) {
        throw "v0.3.1 picker contract missing: $pickerRequirement"
    }
}

if ($sourceStore -notmatch 'SelectedPaths' -or
    $catalogStore -notmatch 'source\.SelectedPaths') {
    throw 'v0.3.1 contract violation: one WebDAV source must persist and scan multiple selected folder roots.'
}

$localAddStart = $sourcesView.IndexOf('private async Task AddLocalSourceAsync')
$localAddEnd = $sourcesView.IndexOf('private async Task AddWebDavSourceAsync', $localAddStart)
$commitStart = $sourcesView.IndexOf('private async Task CommitWebDavEditorAsync')
$commitEnd = $sourcesView.IndexOf('private void RollBackWebDavTarget', $commitStart)
$folderEditStart = $sourcesView.IndexOf('private async void EditWebDavFoldersMenuItem_Click')
$folderEditEnd = $sourcesView.IndexOf('private void RemoveSourceMenuItem_Click', $folderEditStart)

foreach ($segment in @(
    $sourcesView.Substring($localAddStart, $localAddEnd - $localAddStart),
    $sourcesView.Substring($commitStart, $commitEnd - $commitStart),
    $sourcesView.Substring($folderEditStart, $folderEditEnd - $folderEditStart))) {
    if ($segment -match '_catalog\.ScanSourceAsync') {
        throw 'v0.3.1 contract violation: adding/editing a source or folder scope must not auto-scan.'
    }
}

if ($sourcesView -notmatch 'ResolveEditorCredential' -or
    $sourcesView -notmatch 'previousTargetCredential' -or
    $sourcesView -notmatch 'RestoreCredential') {
    throw 'v0.3.1 contract violation: editing/re-authentication must preserve and roll back credentials safely.'
}

if ($sourcesView -notmatch 'Sources_SourceAlreadyExists' -or
    $sourcesView -notmatch 'BuildWebDavSourceId') {
    throw 'v0.3.1 contract violation: editing must guard source-ID collisions.'
}

if ($sourceStore -match 'Password\s*=' -or
    $sourceStore -match 'Password"') {
    throw 'v0.3.1 contract violation: WebDAV passwords must not be persisted in sources.json metadata.'
}

if ($credentialStore -notmatch 'PasswordVault') {
    throw 'v0.3.1 contract violation: persisted WebDAV credentials must remain in Windows PasswordVault.'
}

foreach ($language in @('zh-CN','ja-JP','en-US')) {
    $resources = Get-Content -LiteralPath "src/Eizo.App/Strings/$language/Resources.resw" -Raw
    foreach ($key in @(
        'Sources_EditWebDavTitle',
        'Sources_EditMediaSource',
        'Sources_EditReadFolders',
        'Sources_SaveChanges',
        'Sources_PasswordKeepHint',
        'Sources_SelectCurrentFolder',
        'Sources_LoadingFolders',
        'Sources_NoSubfolders',
        'Sources_WebDavUpdatedFormat',
        'Sources_SourceAlreadyExists',
        'Sources_FolderPickerHint',
        'Sources_AllFolders',
        'Sources_SelectedFoldersFormat',
        'Sources_OpenFolder',
        'Sources_FoldersUpdated',
        'Sources_FoldersUpdatedFormat',
        'Sources_FoldersUpdatedNoScan',
        'Sources_Scanning',
        'Sources_WebDavAddedNoScanFormat',
        'Sources_WebDavUpdatedNoScanFormat')) {
        if ($resources -notmatch ('name="' + [regex]::Escape($key) + '"')) {
            throw "v0.3.1 localization contract missing $key in $language"
        }
    }
}

Write-Host 'Eizo v0.3.1 WebDAV source management contract PASS.'
