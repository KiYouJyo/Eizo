param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required real-content UI file is missing: $relativePath"
    }
    return [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
}

$service = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataService.cs'
$candidate = Read-Text 'src/Eizo.MetadataIntegration/MediaMetadataMatchCandidate.cs'
$coordinator = Read-Text 'src/Eizo.App/Models/MediaScanCoordinator.cs'
$detailXaml = Read-Text 'src/Eizo.App/Views/DetailView.xaml'
$detail = Read-Text 'src/Eizo.App/Views/DetailView.xaml.cs'
$main = Read-Text 'src/Eizo.App/MainWindow.xaml.cs'
$subject = Read-Text 'src/Eizo.App/Models/CatalogSubjectModel.cs'

foreach ($required in @(
    'SearchCandidatesAsync(',
    'MediaMetadataMatchCandidate',
    '_providerResolvers.TryGetValue(')) {
    if (-not $service.Contains($required, [StringComparison]::Ordinal)) {
        throw "Manual-match search API is incomplete: $required"
    }
}

foreach ($required in @(
    'ProviderSubjectId',
    'DisplayTitle',
    'DisplayMeta')) {
    if (-not $candidate.Contains($required, [StringComparison]::Ordinal)) {
        throw "Manual-match candidate model is incomplete: $required"
    }
}

if (-not $coordinator.Contains(
        'SearchMetadataMatchesAsync(',
        [StringComparison]::Ordinal)) {
    throw 'MediaScanCoordinator does not expose manual-match search.'
}

foreach ($required in @(
    'x:Name="MoreButton"',
    'Click="MoreButton_Click"')) {
    if (-not $detailXaml.Contains($required, [StringComparison]::Ordinal)) {
        throw "Detail-page real-content action entry is missing: $required"
    }
}

foreach ($required in @(
    'ShowManualMatchDialogAsync(',
    'SetManualIdentityBinding(',
    'ClearManualIdentityBinding(',
    'RefreshSubjectMetadataAsync(',
    'SubjectUpdated?.Invoke(',
    'SearchMetadataMatchesAsync(',
    '重新刮削',
    '手动匹配')) {
    if (-not $detail.Contains($required, [StringComparison]::Ordinal)) {
        throw "Detail-page real-content workflow is incomplete: $required"
    }
}

if (-not $main.Contains(
        'view.SubjectUpdated +=',
        [StringComparison]::Ordinal)) {
    throw 'Shell does not replace a detail tab after manual match/re-scrape.'
}

if ($subject.Contains(
        'metaParts.Add(metadata.Provider!)',
        [StringComparison]::Ordinal)) {
    throw 'Provider debug identity must not remain in user-facing subject metadata.'
}

Write-Host 'Eizo 0.5.9 real-content UI Slice A contract PASS.'
