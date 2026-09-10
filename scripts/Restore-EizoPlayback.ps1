[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$pinPath = Join-Path $repoRoot 'eng/Eizo.Playback.json'
$dependencyRoot = Join-Path $repoRoot '.deps/Eizo.Playback'
$feedRoot = Join-Path $repoRoot '.packages/Eizo.Playback'
$stampPath = Join-Path $feedRoot '.source-commit'

if (-not (Test-Path -LiteralPath $pinPath -PathType Leaf)) {
    throw "Playback pin file was not found: $pinPath"
}

$pin = Get-Content -LiteralPath $pinPath -Raw | ConvertFrom-Json
$commit = [string]$pin.commit
$version = [string]$pin.version
$repository = [string]$pin.repository

if ([string]::IsNullOrWhiteSpace($commit) -or
    [string]::IsNullOrWhiteSpace($version) -or
    [string]::IsNullOrWhiteSpace($repository)) {
    throw 'Eizo.Playback pin file is incomplete.'
}

$expectedPackages = @(
    "Eizo.Playback.Abstractions.$version.nupkg",
    "Eizo.Playback.Core.$version.nupkg",
    "Eizo.Playback.LibVLC.$version.nupkg",
    "Eizo.Playback.LibVLC.WinUI.$version.nupkg"
)

$feedReady =
    -not $Force -and
    (Test-Path -LiteralPath $stampPath -PathType Leaf) -and
    ((Get-Content -LiteralPath $stampPath -Raw).Trim() -eq $commit) -and
    ($expectedPackages | ForEach-Object {
        Test-Path -LiteralPath (Join-Path $feedRoot $_) -PathType Leaf
    } | Where-Object { -not $_ } | Measure-Object).Count -eq 0

if ($feedReady) {
    Write-Host "Eizo.Playback $version is already restored from $commit."
    exit 0
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git is required to restore Eizo.Playback.'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is required to restore Eizo.Playback.'
}

if (-not (Test-Path -LiteralPath (Join-Path $dependencyRoot '.git'))) {
    if (Test-Path -LiteralPath $dependencyRoot) {
        Remove-Item -LiteralPath $dependencyRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dependencyRoot) | Out-Null

    & git clone --filter=blob:none --no-checkout $repository $dependencyRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to clone Eizo.Playback from $repository."
    }
}

& git -C $dependencyRoot fetch --force origin $commit
if ($LASTEXITCODE -ne 0) {
    throw "Failed to fetch Eizo.Playback commit $commit."
}

& git -C $dependencyRoot checkout --detach --force $commit
if ($LASTEXITCODE -ne 0) {
    throw "Failed to checkout Eizo.Playback commit $commit."
}

Remove-Item -LiteralPath $feedRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $feedRoot | Out-Null

# NuGet.config contains both local Eizo feeds. Keep the sibling source present
# even while this dependency is restored independently.
$metadataFeedRoot = Join-Path $repoRoot '.packages/Eizo.Metadata'
New-Item -ItemType Directory -Force -Path $metadataFeedRoot | Out-Null

$solution = Join-Path $dependencyRoot 'Eizo.Playback.slnx'

& dotnet restore $solution
if ($LASTEXITCODE -ne 0) {
    throw 'Eizo.Playback restore failed.'
}

& dotnet build $solution --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw 'Eizo.Playback Release build failed.'
}

& dotnet pack $solution --configuration Release --no-build --output $feedRoot
if ($LASTEXITCODE -ne 0) {
    throw 'Eizo.Playback pack failed.'
}

foreach ($package in $expectedPackages) {
    $path = Join-Path $feedRoot $package
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Expected Eizo.Playback package was not produced: $package"
    }
}

Set-Content -LiteralPath $stampPath -Value $commit -Encoding ascii -NoNewline

Write-Host "Restored Eizo.Playback $version from commit $commit."
Write-Host "Local NuGet feed: $feedRoot"
