param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-LastExitCode([string]$message) {
    if ($LASTEXITCODE -ne 0) { throw "$message ($LASTEXITCODE)" }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    $runnerTemp = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { [IO.Path]::GetTempPath() } else { $env:RUNNER_TEMP }
    $assets = Join-Path $runnerTemp 'Eizo-v1.0.0-store-upload'
    $appPackages = Join-Path $runnerTemp 'Eizo-AppPackages-v100-store'
    foreach ($path in @($assets, $appPackages)) {
        if (Test-Path $path) { Remove-Item -LiteralPath $path -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }

    Write-Host '== Validate Store package baseline =='
    & ./scripts/Test-ReleaseVersionContract.ps1
    & ./scripts/Test-FirstRunGuideV100Contract.ps1

    [xml]$manifest = Get-Content -LiteralPath 'src/Eizo.App/Package.appxmanifest' -Raw
    $identity = $manifest.Package.Identity
    if ([string]$identity.Version -ne '1.0.0.0') {
        throw "Store package version mismatch: $($identity.Version)"
    }
    $isPlaceholderIdentity = ([string]$identity.Name -eq 'Eizo' -and [string]$identity.Publisher -eq 'CN=AppPublisher')
    if ($isPlaceholderIdentity) {
        Write-Warning 'Building StoreUpload candidate with sideload placeholder identity. Replace with Partner Center identity before submission.'
    }

    Write-Host '== Restore pinned dependencies =='
    & ./scripts/Restore-EizoPlayback.ps1
    & ./scripts/Restore-EizoMetadata.ps1

    Write-Host '== Build StoreUpload package =='
    $msbuildArgs = @(
        'src\Eizo.App\Eizo.App.csproj',
        '/restore',
        '/m',
        '/p:Configuration=Release',
        '/p:Platform=x64',
        '/p:GenerateAppxPackageOnBuild=true',
        '/p:AppxPackageSigningEnabled=false',
        '/p:AppxBundle=Always',
        '/p:AppxBundlePlatforms=x64',
        '/p:UapAppxPackageBuildMode=StoreUpload',
        "/p:AppxPackageDir=$appPackages\"
    )
    & msbuild @msbuildArgs
    Assert-LastExitCode 'StoreUpload build failed'

    $upload = @(Get-ChildItem $appPackages -Recurse -Filter '*.msixupload' -File | Sort-Object Length -Descending) | Select-Object -First 1
    if (-not $upload) {
        $upload = @(Get-ChildItem $appPackages -Recurse -Filter '*.appxupload' -File | Sort-Object Length -Descending) | Select-Object -First 1
    }
    if (-not $upload) {
        throw 'Store upload package (.msixupload/.appxupload) was not produced.'
    }

    $extension = $upload.Extension.ToLowerInvariant()
    $storeUpload = Join-Path $assets "Eizo_1.0.0.0_x64$extension"
    Copy-Item -LiteralPath $upload.FullName -Destination $storeUpload -Force

    $bundle = @(Get-ChildItem $appPackages -Recurse -Filter '*.msixbundle' -File | Sort-Object Length -Descending) | Select-Object -First 1
    if ($bundle) {
        Copy-Item -LiteralPath $bundle.FullName -Destination (Join-Path $assets 'Eizo_1.0.0.0_x64_store-inner.msixbundle') -Force
    }

    $identityStatus = if ($isPlaceholderIdentity) { 'PLACEHOLDER - replace from Partner Center before submission' } else { 'Partner Center identity applied' }
    @(
        'Eizo StoreUpload candidate'
        'Version: 1.0.0.0'
        'Architecture: x64'
        "Identity Name: $($identity.Name)"
        "Publisher: $($identity.Publisher)"
        "Identity status: $identityStatus"
        "Upload: $(Split-Path -Leaf $storeUpload)"
    ) | Set-Content -LiteralPath (Join-Path $assets 'STORE-IDENTITY.txt') -Encoding utf8

    $sumLines = Get-ChildItem $assets -File | Where-Object Extension -in @('.msixupload','.appxupload','.msixbundle') | ForEach-Object {
        "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
    }
    Set-Content -LiteralPath (Join-Path $assets 'SHA256SUMS.txt') -Value $sumLines -Encoding ascii
    Get-Content -LiteralPath (Join-Path $assets 'STORE-IDENTITY.txt')
    Get-Content -LiteralPath (Join-Path $assets 'SHA256SUMS.txt')
    Write-Host "Eizo 1.0.0 StoreUpload candidate PASS. Assets=$assets"
}
finally {
    Pop-Location
}
