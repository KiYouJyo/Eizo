param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required 0.5.13 TMDB integration file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$service = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataService.cs'
$coordinator = Read-Text 'src/Eizo.App/Models/MediaScanCoordinator.cs'
$credentialStore = Read-Text 'src/Eizo.App/Models/MediaCredentialStore.cs'
$verifier = Read-Text 'src/Eizo.App/Models/TmdbConnectionVerifier.cs'
$settingsXaml = Read-Text 'src/Eizo.App/Views/SettingsView.xaml'
$settings = Read-Text 'src/Eizo.App/Views/SettingsView.xaml.cs'
$matchDialog = Read-Text 'src/Eizo.App/Views/MetadataMatchDialog.cs'
$aboutXaml = Read-Text 'src/Eizo.App/Views/AboutView.xaml'
$about = Read-Text 'src/Eizo.App/Views/AboutView.xaml.cs'
$project = Read-Text 'src/Eizo.App/Eizo.App.csproj'
$manifest = Read-Text 'src/Eizo.App/Package.appxmanifest'
$release = Read-Text 'release/release.json'

foreach ($required in @(
    'Eizo.Metadata:TMDB',
    'GetTmdbReadAccessToken()',
    'SaveTmdbReadAccessToken(',
    'RemoveTmdbReadAccessToken()',
    'PasswordVault')) {
    if (-not $credentialStore.Contains($required, [StringComparison]::Ordinal)) {
        throw "TMDB PasswordVault integration is incomplete: $required"
    }
}

foreach ($required in @(
    'EIZO_TMDB_READ_ACCESS_TOKEN',
    'TmdbConfigurationSource',
    'ResolveTmdbReadAccessToken()',
    'MediaCredentialStore.Default',
    'ReloadMetadataService()')) {
    if (-not $coordinator.Contains($required, [StringComparison]::Ordinal)) {
        throw "TMDB production activation is incomplete: $required"
    }
}

foreach ($required in @(
    'AvailableProviders',
    'IsProviderAvailable(',
    'ResolveConfiguredBoundPrimary(',
    'ManualIdentityBinding',
    'PersistedIdentityBinding',
    'return Array.Empty<',
    'MediaMetadataMatchCandidate')) {
    if (-not $service.Contains($required, [StringComparison]::Ordinal)) {
        throw "TMDB provider routing/identity integration is incomplete: $required"
    }
}

foreach ($required in @(
    'https://api.themoviedb.org/3/configuration',
    'AuthenticationHeaderValue(',
    '"Bearer"',
    'KiYouJyo/Eizo/0.5.13')) {
    if (-not $verifier.Contains($required, [StringComparison]::Ordinal)) {
        throw "TMDB connection verification is incomplete: $required"
    }
}

foreach ($required in @(
    'x:Name="TmdbTokenBox"',
    'x:Name="TmdbGetTokenButton"',
    'x:Name="TmdbSaveTokenButton"',
    'x:Name="TmdbVerifyTokenButton"',
    'x:Name="TmdbRemoveTokenButton"')) {
    if (-not $settingsXaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "TMDB settings UI is incomplete: $required"
    }
}

foreach ($required in @(
    'TmdbSaveTokenButton_Click',
    'TmdbVerifyTokenButton_Click',
    'TmdbRemoveTokenButton_Click',
    'TmdbGetTokenButton_Click',
    'https://www.themoviedb.org/settings/api')) {
    if (-not $settings.Contains($required, [StringComparison]::Ordinal)) {
        throw "TMDB settings behavior is incomplete: $required"
    }
}

foreach ($required in @(
    'IsTmdbConfigured',
    '"TMDB"',
    '"tmdb"')) {
    if (-not $matchDialog.Contains($required, [StringComparison]::Ordinal)) {
        throw "TMDB manual matching integration is incomplete: $required"
    }
}

$notice = 'This product uses the TMDB API but is not endorsed or certified by TMDB.'
if (-not $about.Contains($notice, [StringComparison]::Ordinal)) {
    throw 'Required TMDB attribution notice is missing from About.'
}
if (-not $aboutXaml.Contains('TmdbAttributionNotice', [StringComparison]::Ordinal) -or
    -not $about.Contains('https://www.themoviedb.org', [StringComparison]::Ordinal)) {
    throw 'TMDB About attribution/link is incomplete.'
}

foreach ($required in @(
    '<Version>0.5.13</Version>',
    '<AssemblyVersion>0.5.13.0</AssemblyVersion>',
    '<FileVersion>0.5.13.0</FileVersion>',
    '<InformationalVersion>0.5.13</InformationalVersion>')) {
    if (-not $project.Contains($required, [StringComparison]::Ordinal)) {
        throw "Project version is not closed on 0.5.13: $required"
    }
}

if (-not $manifest.Contains('Version="0.5.13.0"', [StringComparison]::Ordinal)) {
    throw 'Package manifest is not 0.5.13.0.'
}

foreach ($required in @(
    '"version": "0.5.13"',
    '"packageVersion": "0.5.13.0"',
    '"en-US": "TMDB Production Integration"')) {
    if (-not $release.Contains($required, [StringComparison]::Ordinal)) {
        throw "Release metadata is not closed on 0.5.13: $required"
    }
}

Write-Host 'Eizo 0.5.13 TMDB production integration contract PASS.'
