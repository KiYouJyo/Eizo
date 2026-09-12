param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required release-version file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$releasePath = Join-Path $repoRoot 'release/release.json'
$release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json
$version = [string]$release.product.version
$packageVersion = [string]$release.product.packageVersion

if ($version -notmatch '^\d+\.\d+\.\d+$' -or
    $packageVersion -ne "$version.0") {
    throw "Invalid release version contract: version=$version packageVersion=$packageVersion"
}

[xml]$project = Read-Text 'src/Eizo.App/Eizo.App.csproj'
[xml]$manifest = Read-Text 'src/Eizo.App/Package.appxmanifest'

$projectVersion = @($project.Project.PropertyGroup | ForEach-Object Version | Where-Object { $_ })[0]
$assemblyVersion = @($project.Project.PropertyGroup | ForEach-Object AssemblyVersion | Where-Object { $_ })[0]
$fileVersion = @($project.Project.PropertyGroup | ForEach-Object FileVersion | Where-Object { $_ })[0]
$informationalVersion = @($project.Project.PropertyGroup | ForEach-Object InformationalVersion | Where-Object { $_ })[0]

$expected = @{
    Project = $projectVersion
    Assembly = $assemblyVersion
    File = $fileVersion
    Informational = $informationalVersion
    Manifest = [string]$manifest.Package.Identity.Version
}

if ($expected.Project -ne $version -or
    $expected.Assembly -ne $packageVersion -or
    $expected.File -ne $packageVersion -or
    $expected.Informational -ne $version -or
    $expected.Manifest -ne $packageVersion) {
    throw "Release version mismatch: $($expected | ConvertTo-Json -Compress) expected=$version/$packageVersion"
}

$appVersionProvider = Read-Text 'src/Eizo.App/AppVersionProvider.cs'
if ($appVersionProvider -match 'const\s+string\s+(?:Version|DisplayVersion)' -or
    $appVersionProvider -match '(?<!\d)0\.3\.\d+(?!\d)') {
    throw 'AppVersionProvider must derive the About-page version dynamically and must not contain a hard-coded 0.3.x product version.'
}
if ($appVersionProvider -notmatch 'DisplayVersion\s*=>\s*\$"v\{Version\}"' -or
    $appVersionProvider -notmatch 'GetCurrentVersion\(\)') {
    throw 'AppVersionProvider dynamic version contract is missing.'
}

foreach ($relativePath in @(
    'src/Eizo.App/Views/AboutView.xaml',
    'src/Eizo.App/Views/AboutView.xaml.cs')) {
    $text = Read-Text $relativePath
    if ($text -match '(?<!\d)v?0\.3\.\d+(?!\d)') {
        throw "About UI contains a hard-coded product version: $relativePath"
    }
}

$resourceFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/Eizo.App') -Recurse -File -Filter '*.resw'
)
foreach ($resource in $resourceFiles) {
    $text = [IO.File]::ReadAllText($resource.FullName, [Text.Encoding]::UTF8)
    if ($text -match '(?<!\d)v?0\.3\.\d+(?!\d)') {
        $relative = [IO.Path]::GetRelativePath($repoRoot, $resource.FullName)
        throw "Localization resource contains a hard-coded product version: $relative"
    }
}

foreach ($relativePath in @(
    "docs/RELEASE-NOTES-v$version.md",
    "docs/RELEASE-NOTES-v$version.ja.md",
    "docs/RELEASE-NOTES-v$version.en.md")) {
    $text = Read-Text $relativePath
    if ($text -notmatch "(?m)^# Eizo v$([regex]::Escape($version))\s*$") {
        throw "Release Notes header does not match v$version: $relativePath"
    }
}

$currentAcceptanceScript = "scripts/Build-EizoV$($version.Replace('.', '').PadLeft(4,'0'))Acceptance.ps1"
# Current naming convention is V0311 for 0.3.11.
$currentAcceptanceScript = "scripts/Build-EizoV0$($version.Split('.')[1])$($version.Split('.')[2])Acceptance.ps1"
$currentAcceptanceWorkflow = ".github/workflows/v0$($version.Split('.')[1])$($version.Split('.')[2])-library-ui-season-family-acceptance.yml"

foreach ($relativePath in @($currentAcceptanceScript, $currentAcceptanceWorkflow)) {
    $text = Read-Text $relativePath
    if (-not $text.Contains($version) -or -not $text.Contains($packageVersion)) {
        throw "Current acceptance asset is not pinned to $version/$packageVersion: $relativePath"
    }
    if ($text -match '(?<!\d)0\.3\.(?:1|7|8|9|10)(?!\d)') {
        throw "Current acceptance asset contains a stale product version: $relativePath"
    }
}

Write-Host "Release version contract PASS: Eizo $version / $packageVersion"
Write-Host 'About-page version source: installed Package / Assembly (no hard-coded product version).'
