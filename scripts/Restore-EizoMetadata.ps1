[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$pinPath = Join-Path $repoRoot 'eng/Eizo.Metadata.Recognition.json'
$dependencyRoot = Join-Path $repoRoot '.deps/Eizo.Metadata'
$feedRoot = Join-Path $repoRoot '.packages/Eizo.Metadata'
$stampPath = Join-Path $feedRoot '.source-commit'

if (-not (Test-Path -LiteralPath $pinPath -PathType Leaf)) {
    throw "Recognition pin file was not found: $pinPath"
}

$pin = Get-Content -LiteralPath $pinPath -Raw | ConvertFrom-Json
$commit = [string]$pin.commit
$version = [string]$pin.version
$repository = [string]$pin.repository

if ([string]::IsNullOrWhiteSpace($commit) -or
    [string]::IsNullOrWhiteSpace($version) -or
    [string]::IsNullOrWhiteSpace($repository)) {
    throw 'Eizo.Metadata.Recognition pin file is incomplete.'
}

$expectedPackage = "Eizo.Metadata.Recognition.$version.nupkg"

$feedReady =
    -not $Force -and
    (Test-Path -LiteralPath $stampPath -PathType Leaf) -and
    ((Get-Content -LiteralPath $stampPath -Raw).Trim() -eq $commit) -and
    (Test-Path -LiteralPath (Join-Path $feedRoot $expectedPackage) -PathType Leaf)

if ($feedReady) {
    Write-Host "Eizo.Metadata.Recognition $version is already restored from $commit."
    exit 0
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git is required to restore Eizo.Metadata.Recognition.'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is required to restore Eizo.Metadata.Recognition.'
}

if (-not (Test-Path -LiteralPath (Join-Path $dependencyRoot '.git'))) {
    if (Test-Path -LiteralPath $dependencyRoot) {
        Remove-Item -LiteralPath $dependencyRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dependencyRoot) | Out-Null

    & git clone --filter=blob:none --no-checkout $repository $dependencyRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to clone Eizo.Metadata from $repository."
    }
}

& git -C $dependencyRoot fetch --force origin $commit
if ($LASTEXITCODE -ne 0) {
    throw "Failed to fetch Eizo.Metadata commit $commit."
}

& git -C $dependencyRoot checkout --detach --force $commit
if ($LASTEXITCODE -ne 0) {
    throw "Failed to checkout Eizo.Metadata commit $commit."
}

Remove-Item -LiteralPath $feedRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $feedRoot | Out-Null

# NuGet.config contains both local Eizo feeds. Keep the sibling source present
# even when Recognition is restored before Playback.
$playbackFeedRoot = Join-Path $repoRoot '.packages/Eizo.Playback'
New-Item -ItemType Directory -Force -Path $playbackFeedRoot | Out-Null

$project = Join-Path $dependencyRoot 'src/Eizo.Metadata.Recognition/Eizo.Metadata.Recognition.csproj'

& dotnet restore $project
if ($LASTEXITCODE -ne 0) {
    throw 'Eizo.Metadata.Recognition restore failed.'
}

& dotnet build $project --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw 'Eizo.Metadata.Recognition Release build failed.'
}

& dotnet pack $project --configuration Release --no-build --output $feedRoot
if ($LASTEXITCODE -ne 0) {
    throw 'Eizo.Metadata.Recognition pack failed.'
}

$packagePath = Join-Path $feedRoot $expectedPackage
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw "Expected Eizo.Metadata.Recognition package was not produced: $expectedPackage"
}

Set-Content -LiteralPath $stampPath -Value $commit -Encoding ascii -NoNewline

Write-Host "Restored Eizo.Metadata.Recognition $version from commit $commit."
Write-Host "Local NuGet feed: $feedRoot"
