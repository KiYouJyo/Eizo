$ErrorActionPreference = 'Stop'

$sourcesView = Get-Content -LiteralPath 'src/Eizo.App/Views/SourcesView.xaml.cs' -Raw
$sourceStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaSourceStore.cs' -Raw
$credentialStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaCredentialStore.cs' -Raw
$catalogStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaCatalogStore.cs' -Raw
$folderPicker = Get-Content -LiteralPath 'src/Eizo.App/Views/WebDavFolderPickerWindow.cs' -Raw

foreach ($required in @(
    'EditWebDavMenuItem_Click',
    'ShowWebDavEditorAsync',
    'CommitWebDavEditorAsync',
    'BrowseWebDavFoldersAsync',
    'RollBackWebDavTarget',
    'InlineWebDavCredentialProvider',
    'Sources_EditWebDavTitle',
    'Sources_SelectCurrentFolder')) {
    if ($sourcesView -notmatch [regex]::Escape($required)) {
        throw "v0.3.1 WebDAV source-management contract missing: $required"
    }
}

if ($sourcesView -notmatch 'WebDavFolderPickerWindow' -or
    $sourcesView -notmatch 'WebDavMediaSourceProvider\(' -or
    $folderPicker -notmatch '_provider\.ListAsync\(') {
    throw 'v0.3.1 contract violation: remote folder browsing must use the real WebDAV provider/ListAsync path.'
}

foreach ($pickerRequirement in @(
    'BreadcrumbBar',
    'CheckBox',
    'ScrollViewer.SetHorizontalScrollMode',
    'ScrollMode.Disabled',
    'ScrollViewer.SetHorizontalScrollBarVisibility',
    'ScrollBarVisibility.Disabled',
    'Sources_SelectedFoldersFormat',
    'DoubleTapped')) {
    if ($folderPicker -notmatch [regex]::Escape($pickerRequirement)) {
        throw "v0.3.1 picker contract missing: $pickerRequirement"
    }
}

if ($sourceStore -notmatch 'SelectedPaths' -or
    $catalogStore -notmatch 'source\.SelectedPaths') {
    throw 'v0.3.1 contract violation: one WebDAV source must persist and scan multiple selected folder roots.'
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
        'Sources_SaveChanges',
        'Sources_PasswordKeepHint',
        'Sources_BrowseFolders',
        'Sources_ParentFolder',
        'Sources_SelectCurrentFolder',
        'Sources_LoadingFolders',
        'Sources_NoSubfolders',
        'Sources_WebDavUpdatedFormat',
        'Sources_SourceAlreadyExists',
        'Sources_FolderPickerTitle',
        'Sources_FolderPickerHint',
        'Sources_AllFolders',
        'Sources_SelectedFoldersFormat',
        'Sources_OpenFolder')) {
        if ($resources -notmatch ('name="' + [regex]::Escape($key) + '"')) {
            throw "v0.3.1 localization contract missing $key in $language"
        }
    }
}

Write-Host 'Eizo v0.3.1 WebDAV source management contract PASS.'
