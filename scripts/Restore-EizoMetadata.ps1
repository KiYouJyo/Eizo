[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$pinPath = Join-Path $repoRoot 'eng/Eizo.Metadata.json'
$dependencyRoot = Join-Path $repoRoot '.deps/Eizo.Metadata'
$feedRoot = Join-Path $repoRoot '.packages/Eizo.Metadata'
$stampPath = Join-Path $feedRoot '.source-commit'

if (-not (Test-Path -LiteralPath $pinPath -PathType Leaf)) {
    throw "Eizo.Metadata pin file was not found: $pinPath"
}

$pin = Get-Content -LiteralPath $pinPath -Raw | ConvertFrom-Json
$commit = [string]$pin.commit
$version = [string]$pin.version
$repository = [string]$pin.repository
$package = [string]$pin.package
$sourceStamp = "$commit/$version/$package"
$expectedPackage = "$package.$version.nupkg"

if ([string]::IsNullOrWhiteSpace($commit) -or
    [string]::IsNullOrWhiteSpace($version) -or
    [string]::IsNullOrWhiteSpace($repository) -or
    [string]::IsNullOrWhiteSpace($package)) {
    throw 'Eizo.Metadata pin file is incomplete.'
}

$feedReady =
    -not $Force -and
    (Test-Path -LiteralPath $stampPath -PathType Leaf) -and
    ((Get-Content -LiteralPath $stampPath -Raw).Trim() -eq $sourceStamp) -and
    (Test-Path -LiteralPath (Join-Path $feedRoot $expectedPackage) -PathType Leaf)

if ($feedReady) {
    Write-Host "Eizo.Metadata $version is already restored from $commit."
    exit 0
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git is required to restore Eizo.Metadata.'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is required to restore Eizo.Metadata.'
}

$checkoutCreated = $false
if (-not (Test-Path -LiteralPath (Join-Path $dependencyRoot '.git'))) {
    if (Test-Path -LiteralPath $dependencyRoot) {
        $resolved = [IO.Path]::GetFullPath($dependencyRoot)
        $expected = [IO.Path]::GetFullPath((Join-Path $repoRoot '.deps/Eizo.Metadata'))
        if ($resolved -ne $expected) { throw 'Unexpected Eizo.Metadata cleanup path.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dependencyRoot) | Out-Null
    & git clone --filter=blob:none --no-checkout $repository $dependencyRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to clone Eizo.Metadata from $repository."
    }
    $checkoutCreated = $true
}

& git -C $dependencyRoot fetch --force origin $commit
if ($LASTEXITCODE -ne 0) {
    throw "Failed to fetch Eizo.Metadata commit $commit."
}

if (-not $checkoutCreated) {
    $dirty = & git -C $dependencyRoot status --porcelain
    if ($dirty) {
        throw 'Eizo.Metadata dependency has local changes; refusing to overwrite them.'
    }
}

& git -C $dependencyRoot checkout --detach --force $commit
if ($LASTEXITCODE -ne 0) {
    throw "Failed to checkout Eizo.Metadata commit $commit."
}

New-Item -ItemType Directory -Force -Path $feedRoot | Out-Null
$project = Join-Path $dependencyRoot 'src/Eizo.Metadata.Recognition/Eizo.Metadata.Recognition.csproj'

& dotnet restore $project
if ($LASTEXITCODE -ne 0) {
    throw 'Eizo.Metadata Recognition restore failed.'
}

& dotnet pack $project --configuration Release --no-restore --output $feedRoot
if ($LASTEXITCODE -ne 0) {
    throw 'Eizo.Metadata Recognition pack failed.'
}

$packagePath = Join-Path $feedRoot $expectedPackage
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw "Expected Eizo.Metadata package was not produced: $expectedPackage"
}

Set-Content -LiteralPath $stampPath -Value $sourceStamp -Encoding ascii -NoNewline
Write-Host "Restored Eizo.Metadata $version from commit $commit."
Write-Host "Local NuGet feed: $feedRoot"
