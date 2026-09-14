param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required identity-binding file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$hint = Read-Text 'src/Eizo.MetadataIntegration/MediaIdentityBindingHint.cs'
$bindingStore = Read-Text 'src/Eizo.App/Models/MediaIdentityBindingStore.cs'
$service = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataService.cs'
$catalog = Read-Text 'src/Eizo.App/Models/MediaCatalogStore.cs'
$snapshot = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataSnapshot.cs'
$catalogView = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'

foreach ($required in @(
    'MediaIdentityBindingHint(',
    'PrimaryProvider',
    'ExternalIds',
    'IsManual')) {
    if (-not $hint.Contains($required, [StringComparison]::Ordinal)) {
        throw "Identity binding hint is incomplete: $required"
    }
}

foreach ($required in @(
    'media-identity-bindings.json',
    'UpsertAutomatic(',
    'SetManual(',
    'ClearManual(',
    'ManualProviders',
    'File.Move(')) {
    if (-not $bindingStore.Contains($required, [StringComparison]::Ordinal)) {
        throw "Persistent identity binding store is incomplete: $required"
    }
}

foreach ($required in @(
    'EnrichBoundProviderAsync(',
    'ManualIdentityBinding',
    'PersistedIdentityBinding',
    '_providers',
    'binding.ExternalIds.TryGetValue')) {
    if (-not $service.Contains($required, [StringComparison]::Ordinal)) {
        throw "Metadata service binding integration is incomplete: $required"
    }
}

foreach ($required in @(
    'IdentityBindings.GetHint(',
    'IdentityBindings.UpsertAutomatic(',
    'SetManualIdentityBinding(',
    'ClearManualIdentityBinding(',
    'InvalidateMetadataForMedia(')) {
    if (-not $catalog.Contains($required, [StringComparison]::Ordinal)) {
        throw "Catalog binding lifecycle is incomplete: $required"
    }
}

foreach ($required in @(
    'public string? IdentityBindingProvider { get; init; }',
    'public bool IdentityBindingManual { get; init; }')) {
    if (-not $snapshot.Contains($required, [StringComparison]::Ordinal)) {
        throw "Binding diagnostics are missing from snapshot: $required"
    }
}

foreach ($column in @(
    'IdentityBindingProvider',
    'IdentityBindingManual')) {
    if (-not $catalogView.Contains($column, [StringComparison]::Ordinal)) {
        throw "Binding diagnostics are missing from recognition report: $column"
    }
}

Write-Host 'Eizo 0.5.7 persistent identity binding contract PASS.'
