param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required 0.5.12 UI close-out file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$catalogXaml = Read-Text 'src/Eizo.App/Views/CatalogView.xaml'
$catalog = Read-Text 'src/Eizo.App/Views/CatalogView.xaml.cs'
$detailXaml = Read-Text 'src/Eizo.App/Views/DetailView.xaml'
$detail = Read-Text 'src/Eizo.App/Views/DetailView.xaml.cs'
$homeView = Read-Text 'src/Eizo.App/Views/HomeView.xaml.cs'
$project = Read-Text 'src/Eizo.App/Eizo.App.csproj'
$manifest = Read-Text 'src/Eizo.App/Package.appxmanifest'
$release = Read-Text 'release/release.json'

foreach ($required in @(
    'x:Name="ActionStatusBar"',
    'IsClosable="True"')) {
    if (-not $catalogXaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "Catalog action-state UI is incomplete: $required"
    }
}

foreach ($required in @(
    'Text="{Binding SourceLabel}"',
    'Visibility="Collapsed"')) {
    if (-not $catalogXaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "Catalog source diagnostics are not safely hidden: $required"
    }
}

foreach ($required in @(
    'ShowActionStatus(',
    'InfoBarSeverity.Success',
    'InfoBarSeverity.Warning',
    'InfoBarSeverity.Error')) {
    if (-not $catalog.Contains($required, [StringComparison]::Ordinal)) {
        throw "Catalog metadata-action state handling is incomplete: $required"
    }
}

if ($catalog.Contains(
        'RecognitionLabel(item) ?? CategoryLabel(category)',
        [StringComparison]::Ordinal)) {
    throw 'Recognition confidence is still exposed as a normal catalog-card badge.'
}

foreach ($required in @(
    'x:Name="ReleaseStatBadge"',
    'x:Name="EpisodeStatBadge"',
    'x:Name="SourceStatBadge"',
    'x:Name="MetadataActionStatusBar"',
    'x:Name="EpisodeEmptyStateText"')) {
    if (-not $detailXaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "Detail-page close-out UI is incomplete: $required"
    }
}

foreach ($required in @(
    'NativeTitleText.Visibility',
    'MetaText.Visibility',
    'OverviewText.Visibility',
    'ReleaseStatBadge.Visibility',
    'EpisodeEmptyStateText.Visibility',
    'ShowMetadataActionStatus(')) {
    if (-not $detail.Contains($required, [StringComparison]::Ordinal)) {
        throw "Detail-page real-content state handling is incomplete: $required"
    }
}

foreach ($forbidden in @(
    'SourceStatText.Text = L("示例", "サンプル", "Sample")',
    '"一级魔法使考试"',
    '"一級魔法使試験"')) {
    if ($detail.Contains($forbidden, [StringComparison]::Ordinal)) {
        throw "Legacy sample detail content remains reachable: $forbidden"
    }
}

if ($homeView.Contains(
        'DetailRequested?.Invoke(',
        [StringComparison]::Ordinal) -or
    $homeView.Contains(
        'PlayRequested?.Invoke(',
        [StringComparison]::Ordinal)) {
    throw 'Home hero still routes to the legacy title-only sample detail/player path.'
}

foreach ($required in @(
    '<Version>0.5.12</Version>',
    '<AssemblyVersion>0.5.12.0</AssemblyVersion>',
    '<FileVersion>0.5.12.0</FileVersion>',
    '<InformationalVersion>0.5.12</InformationalVersion>')) {
    if (-not $project.Contains($required, [StringComparison]::Ordinal)) {
        throw "Project version is not closed on 0.5.12: $required"
    }
}

if (-not $manifest.Contains(
        'Version="0.5.12.0"',
        [StringComparison]::Ordinal)) {
    throw 'Package manifest is not 0.5.12.0.'
}

foreach ($required in @(
    '"version": "0.5.12"',
    '"packageVersion": "0.5.12.0"',
    '"en-US": "Real-content UI Close-out"')) {
    if (-not $release.Contains($required, [StringComparison]::Ordinal)) {
        throw "Release metadata is not closed on 0.5.12: $required"
    }
}

Write-Host 'Eizo 0.5.12 real-content UI close-out contract PASS.'
