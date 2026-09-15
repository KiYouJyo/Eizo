param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required 0.5.14 scraping architecture file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$coordinator = Read-Text 'src/Eizo.App/Models/MediaScanCoordinator.cs'
$catalog = Read-Text 'src/Eizo.App/Models/MediaCatalogStore.cs'
$bindings = Read-Text 'src/Eizo.App/Models/MediaIdentityBindingStore.cs'
$projection = Read-Text 'src/Eizo.MetadataIntegration/MediaModelProjection.cs'
$dialog = Read-Text 'src/Eizo.App/Views/MetadataMatchDialog.cs'
$project = Read-Text 'src/Eizo.App/Eizo.App.csproj'
$manifest = Read-Text 'src/Eizo.App/Package.appxmanifest'
$release = Read-Text 'release/release.json'

if (-not $coordinator.Contains('EnableBangumi: false', [StringComparison]::Ordinal)) {
    throw 'Production library metadata must not create the Bangumi provider.'
}

foreach ($required in @(
    'provider: "tmdb"',
    'Library metadata is now provided exclusively by TMDB')) {
    if (-not $dialog.Contains($required, [StringComparison]::Ordinal)) {
        throw "TMDB-only manual matching is incomplete: $required"
    }
}
foreach ($forbidden in @(
    'ProviderOption',
    'providerBox',
    '"Bangumi"')) {
    if ($dialog.Contains($forbidden, [StringComparison]::Ordinal)) {
        throw "Legacy multi-provider manual matching remains: $forbidden"
    }
}

foreach ($required in @(
    'MetadataSubjectParallelism = 3',
    'MetadataSeasonParallelism = 2',
    'MetadataSubjectKey(',
    '.GroupBy(',
    'GetTmdbHint(',
    'SemaphoreSlim(',
    'Task.WhenAll(',
    'representative item establishes the persisted TMDB identity')) {
    if (-not $catalog.Contains($required, [StringComparison]::Ordinal)) {
        throw "Subject/season batch scraping is incomplete: $required"
    }
}

foreach ($required in @(
    'GetTmdbHint(',
    'ids.Remove("bangumi")',
    'manualProviders.Remove("bangumi")',
    'primary = "tmdb"')) {
    if (-not $bindings.Contains($required, [StringComparison]::Ordinal)) {
        throw "TMDB identity migration is incomplete: $required"
    }
}

if (-not $projection.Contains('externalIds.Remove("bangumi")', [StringComparison]::Ordinal)) {
    throw 'Projected local-library identity still carries legacy Bangumi IDs after TMDB enrichment.'
}

# Bangumi remains a separate discovery/community product module.
if (-not $project.Contains('../Eizo.Bangumi/Eizo.Bangumi.csproj', [StringComparison]::Ordinal)) {
    throw 'Bangumi product module must remain available after metadata decoupling.'
}

[xml]$projectXml = $project
[xml]$manifestXml = $manifest
$releaseJson = $release | ConvertFrom-Json

$currentProductVersion = [Version]::Parse(
    [string]@($projectXml.Project.PropertyGroup | ForEach-Object Version | Where-Object { $_ })[0])
$currentPackageVersion = [Version]::Parse(
    [string]$manifestXml.Package.Identity.Version)
$currentReleaseVersion = [Version]::Parse(
    [string]$releaseJson.product.version)

foreach ($versionCheck in @(
    @{ Name = 'Project'; Value = $currentProductVersion },
    @{ Name = 'Package'; Value = $currentPackageVersion },
    @{ Name = 'Release'; Value = $currentReleaseVersion })) {
    if ($versionCheck.Value -lt [Version]'0.5.14') {
        throw "$($versionCheck.Name) version regressed below the 0.5.14 scraping-architecture baseline: $($versionCheck.Value)"
    }
}

Write-Host 'Eizo 0.5.14 scraping architecture simplification contract PASS.'
