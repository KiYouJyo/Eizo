param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot

function Read-Text([string] $relativePath) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Bangumi account contract missing file: $relativePath"
    }

    return [IO.File]::ReadAllText(
        $path,
        [Text.Encoding]::UTF8)
}

$client = Read-Text 'src/Eizo.Bangumi/BangumiApiClient.cs'
$repository = Read-Text 'src/Eizo.Bangumi/BangumiRepository.cs'
$parser = Read-Text 'src/Eizo.Bangumi/BangumiJsonParser.cs'
$credentialStore = Read-Text 'src/Eizo.App/Models/BangumiAccountCredentialStore.cs'
$accountService = Read-Text 'src/Eizo.App/Models/BangumiAccountService.cs'
$following = Read-Text 'src/Eizo.App/Views/BangumiFollowingView.xaml.cs'
$dialog = Read-Text 'src/Eizo.App/Views/BangumiAccountDialogService.cs'
$settings = Read-Text 'src/Eizo.App/Views/SettingsView.xaml.cs'
$appSettings = Read-Text 'src/Eizo.App/AppSettingsStore.cs'
$shell = Read-Text 'src/Eizo.App/MainWindow.xaml.cs'

foreach ($required in @(
    '"v0/me"',
    'Authorization',
    '"Bearer"',
    '/collections?subject_type=2&type=')) {
    if (-not $client.Contains(
            $required,
            [StringComparison]::Ordinal)) {
        throw "Bangumi authenticated client contract missing: $required"
    }
}

foreach ($required in @(
    'GetMyselfAsync',
    'GetFollowingAsync',
    'BangumiCollectionType.Doing',
    'ParseUserProfile',
    'ParseUserCollectionPage')) {
    $haystack = $repository + $parser
    if (-not $haystack.Contains(
            $required,
            [StringComparison]::Ordinal)) {
        throw "Bangumi authenticated repository/parser contract missing: $required"
    }
}

foreach ($required in @(
    'Windows.Security.Credentials',
    'PasswordVault',
    '"Eizo.Bangumi"',
    '"access-token"',
    'SaveAccessToken',
    'RemoveAccessToken')) {
    if (-not $credentialStore.Contains(
            $required,
            [StringComparison]::Ordinal)) {
        throw "Bangumi credential-locker contract missing: $required"
    }
}

if ($appSettings.Contains(
        'BangumiAccessToken',
        [StringComparison]::Ordinal) -or
    $appSettings.Contains(
        'AccessToken',
        [StringComparison]::Ordinal)) {
    throw 'Bangumi Access Token must not be persisted in AppSettings.'
}

foreach ($required in @(
    'ConnectAsync',
    'GetProfileAsync',
    'GetFollowingAsync',
    'Disconnect',
    'HttpStatusCode.Unauthorized')) {
    if (-not $accountService.Contains(
            $required,
            [StringComparison]::Ordinal)) {
        throw "Bangumi account-session contract missing: $required"
    }
}

foreach ($required in @(
    'BangumiAccountDialogService.ShowConnectAsync',
    'GetCollectionAsync',
    'CollectionSectionList_SelectionChanged',
    'BangumiCollectionType.Wish',
    'BangumiCollectionType.Done',
    'BangumiCollectionType.Doing',
    'BangumiCollectionType.OnHold',
    'BangumiCollectionType.Dropped',
    'LoadMoreButton_Click',
    'SubjectRequested?.Invoke')) {
    if (-not $following.Contains(
            $required,
            [StringComparison]::Ordinal)) {
        throw "Bangumi My Following view contract missing: $required"
    }
}

foreach ($forbidden in @(
    'https://next.bgm.tv/demo/access-token',
    'PasswordBox',
    'Bangumi_ManualLogin',
    'ShowManualConnectAsync')) {
    if ($dialog.Contains(
            $forbidden,
            [StringComparison]::Ordinal)) {
        throw "Legacy manual Bangumi token login UI remains: $forbidden"
    }
}

foreach ($required in @(
    'BangumiConnectButton_Click',
    'BangumiDisconnectButton_Click',
    'Bangumi_AccountConnectedFormat')) {
    if (-not $settings.Contains(
            $required,
            [StringComparison]::Ordinal)) {
        throw "Bangumi settings account contract missing: $required"
    }
}

foreach ($required in @(
    'new BangumiFollowingView()',
    'WireBangumiFollowingView')) {
    if (-not $shell.Contains(
            $required,
            [StringComparison]::Ordinal)) {
        throw "Bangumi My Following shell routing contract missing: $required"
    }
}

Write-Host 'Eizo v0.4.6 Bangumi account and five-state collection contract PASS.'
