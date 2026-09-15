param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required first-run guide file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$xaml = Read-Text 'src/Eizo.App/Views/FirstRunGuideHost.xaml'
$code = Read-Text 'src/Eizo.App/Views/FirstRunGuideHost.xaml.cs'
$state = Read-Text 'src/Eizo.App/Models/FirstRunGuideState.cs'
$service = Read-Text 'src/Eizo.App/Models/FirstRunExperienceService.cs'
$window = Read-Text 'src/Eizo.App/MainWindow.xaml'
$startup = Read-Text 'src/Eizo.App/MainWindow.Startup.cs'

foreach ($name in @(
    'WelcomeStep',
    'SourcesStep',
    'TmdbStep',
    'BangumiStep',
    'PlaybackStep',
    'CompleteStep')) {
    if (-not $xaml.Contains("x:Name=`"$name`"")) {
        throw "First-run guide is missing step surface: $name"
    }
}

if (-not $xaml.Contains('x:Name="SourceSetupHost"') -or
    -not $code.Contains('SourceSetupHost.Content = new SourcesView()') -or
    -not $code.Contains('MediaSourceStore.Default')) {
    throw 'First-run media-source setup must lazily reuse the production SourcesView/store.'
}

foreach ($required in @(
    'MediaCredentialStore.Default',
    'TmdbConnectionVerifier.CheckAsync',
    'ReloadMetadataService',
    'BangumiOAuthService.Default.StartAsync',
    'BangumiAccountService',
    'AppSettingsStore.Update',
    'PreferredSecondarySubtitleLanguage',
    'FullscreenControlsTimeoutSeconds',
    'StartInitialScansAsync')) {
    if (-not $code.Contains($required)) {
        throw "First-run guide integration is missing: $required"
    }
}

if (-not $service.Contains('CurrentVersion = 1') -or
    -not $service.Contains('CompletedGuideVersion') -or
    -not $state.Contains('CompletedGuideVersion')) {
    throw 'First-run guide lifecycle state contract is missing.'
}

if (-not $window.Contains('FirstRunGuideHost') -or
    -not $startup.Contains('ShouldShowAutomatically') -or
    -not $startup.Contains('FirstRunGuideHost.Show()')) {
    throw 'MainWindow startup does not host/launch the first-run guide.'
}

if (-not $code.Contains('VirtualKey.Escape') -or
    -not $code.Contains('TryMarkCompleted')) {
    throw 'First-run interruption/completion semantics are missing.'
}

Write-Host 'Eizo 1.0 first-run guide contract PASS.'
Write-Host 'Six steps: Welcome / Sources / TMDB / Bangumi / Playback / Finish.'
