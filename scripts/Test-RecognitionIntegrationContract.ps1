$ErrorActionPreference = 'Stop'

$pinPath = 'eng/Eizo.Metadata.Recognition.json'
$projectPath = 'src/Eizo.App/Eizo.App.csproj'
$modelPath = 'src/Eizo.App/Models/CatalogMediaItemModel.cs'
$storePath = 'src/Eizo.App/Models/MediaCatalogStore.cs'
$nugetPath = 'NuGet.config'

$pin = Get-Content -LiteralPath $pinPath -Raw | ConvertFrom-Json
$project = Get-Content -LiteralPath $projectPath -Raw
$model = Get-Content -LiteralPath $modelPath -Raw
$store = Get-Content -LiteralPath $storePath -Raw
$nuget = Get-Content -LiteralPath $nugetPath -Raw

if ([string]$pin.repository -ne 'https://github.com/KiYouJyo/Eizo.Metadata.git' -or
    [string]$pin.commit -notmatch '^[0-9a-f]{40}$' -or
    [string]$pin.version -ne '0.1.0') {
    throw 'Recognition integration contract violation: dependency pin is invalid.'
}

if ($project -notmatch '<PackageReference Include="Eizo\.Metadata\.Recognition" Version="0\.1\.0"') {
    throw 'Recognition integration contract violation: Eizo.App package reference is missing or unpinned.'
}

if ($nuget -notmatch 'Eizo\.Metadata local' -or
    $nuget -notmatch '\.packages/Eizo\.Metadata') {
    throw 'Recognition integration contract violation: local metadata NuGet feed is missing.'
}

foreach ($required in @(
    'CatalogRecognitionModel',
    'CatalogRecognitionTitleCandidateModel',
    'CatalogRecognitionMediaKind',
    'CatalogRecognitionSpecialKind',
    'CatalogRecognitionEpisodePart',
    'CatalogRecognitionConfidenceLevel',
    'IsAmbiguous',
    'TitleCandidates')) {
    if (-not $model.Contains($required)) {
        throw "Recognition integration contract violation: catalog DTO is missing $required."
    }
}

if ($model.Contains('RecognitionEvidence') -or $store -match '\.Evidence\b') {
    throw 'Recognition integration contract violation: parser evidence must not be persisted in catalog DTOs.'
}

if ($store -notmatch 'RecognitionEngine\s+Recognizer' -or
    $store -notmatch 'new RecognitionRequest\(logicalPath\)') {
    throw 'Recognition integration contract violation: catalog scan is not invoking the offline recognizer.'
}

if ($store -notmatch 'entry\.RelativePath' -or
    $store -notmatch 'Path\.GetRelativePath\(root, info\.FullName\)') {
    throw 'Recognition integration contract violation: local/WebDAV scans must recognize logical relative paths.'
}

if ($store -match 'RecognitionRequest\([^\r\n]*(Locator|FullName)') {
    throw 'Recognition integration contract violation: absolute media locators must not be passed to Recognition.'
}

if ($store -notmatch 'Task\.Run\([\s\S]*CreateRemoteItems' -or
    $store -notmatch 'ScanSourceAsync[\s\S]*Task\.Run\([\s\S]*ScanLocalSource') {
    throw 'Recognition integration contract violation: bulk Recognition must remain off the UI thread.'
}

if ($store -notmatch 'SchemaVersion:\s*2' -or
    $store -notmatch 'SchemaVersion is 1 or 2') {
    throw 'Recognition integration contract violation: catalog schema v2 migration support is incomplete.'
}

if ($store -notmatch 'exception is not OutOfMemoryException' -or
    $store -notmatch 'SourceTitle and Location remain sufficient for raw playback') {
    throw 'Recognition integration contract violation: recognition failure must preserve raw media usability.'
}

if ($store -match 'Category:\s*Map' -or
    $store -match 'Category:\s*recognition') {
    throw 'Recognition integration contract violation: Recognition must not infer anime/drama catalog category.'
}

Write-Host 'Eizo Recognition Stage 7 integration contract PASS.'
