$ErrorActionPreference = 'Stop'

$sourcesView = Get-Content -LiteralPath 'src/Eizo.App/Views/SourcesView.xaml.cs' -Raw
$sourceStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaSourceStore.cs' -Raw
$credentialStore = Get-Content -LiteralPath 'src/Eizo.App/Models/MediaCredentialStore.cs' -Raw

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

if ($sourcesView -notmatch 'provider\.ListAsync\(' -or
    $sourcesView -notmatch 'WebDavMediaSourceProvider\(') {
    throw 'v0.3.1 contract violation: remote folder browsing must use the real WebDAV provider/ListAsync path.'
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
        'Sources_SourceAlreadyExists')) {
        if ($resources -notmatch ('name="' + [regex]::Escape($key) + '"')) {
            throw "v0.3.1 localization contract missing $key in $language"
        }
    }
}

Write-Host 'Eizo v0.3.1 WebDAV source management contract PASS.'
