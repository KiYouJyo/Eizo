param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$xaml = Read-Text 'src/Eizo.App/Views/AboutView.xaml'
$code = Read-Text 'src/Eizo.App/Views/AboutView.xaml.cs'
$service = Read-Text 'src/Eizo.App/ComponentUpdateService.cs'

$columns = 'ColumnDefinitions="2*,110,32,110,190,150,150,150"'
if ([regex]::Matches($xaml, [regex]::Escape($columns)).Count -lt 2) {
    throw 'Eizo and Metadata update cards no longer share the same column contract.'
}

foreach ($required in @(
    'x:Name="ReleaseNotesButton"',
    'Grid.Column="6"',
    'x:Name="RecognitionReleaseNotesButton"',
    'Click="RecognitionReleaseNotesButton_Click"',
    'x:Name="CheckRecognitionUpdateButton"')) {
    if (-not $xaml.Contains($required)) {
        throw "Update management UI contract is missing: $required"
    }
}

if ($xaml -notmatch 'RecognitionReleaseNotesButton"[\s\S]{0,200}Grid\.Column="6"[\s\S]{0,200}MinWidth="132"' -or
    $xaml -notmatch 'CheckRecognitionUpdateButton"[\s\S]{0,200}Grid\.Column="7"[\s\S]{0,200}MinWidth="132"') {
    throw 'Metadata release notes / update actions are not aligned with the Eizo card.'
}

foreach ($required in @(
    'MetadataReleasesUri',
    'RecognitionReleaseNotesButton_Click',
    'RecognitionUpdateService.LatestRelease',
    'RecognitionReleaseNotesButton.Content = T("About_ReleaseNotes")')) {
    if (-not $code.Contains($required)) {
        throw "Metadata release notes behavior is missing: $required"
    }
}

foreach ($required in @(
    'public GitHubReleaseInfo? LatestRelease => _pendingRelease;',
    '_pendingRelease = release;')) {
    if (-not $service.Contains($required)) {
        throw "Component release metadata contract is missing: $required"
    }
}

Write-Host 'Eizo 0.6.0 update management contract PASS.'
