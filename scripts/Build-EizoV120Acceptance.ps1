param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-LastExitCode([string]$message) {
    if ($LASTEXITCODE -ne 0) {
        throw "$message ($LASTEXITCODE)"
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

Push-Location $repoRoot
try {
    $release = Get-Content -LiteralPath 'release/release.json' -Raw | ConvertFrom-Json
    if ([string]$release.product.version -ne '1.2.0' -or
        [string]$release.product.packageVersion -ne '1.2.0.0') {
        throw "Eizo v1.2.0 acceptance requires release 1.2.0 / 1.2.0.0."
    }

    Write-Host '== Validate Eizo 1.2.0 unified card UI =='
    & ./scripts/Test-CardUiV120Contract.ps1
    & ./scripts/Test-CacheSystemContract.ps1
    & ./scripts/Test-LibraryAggregationUiContract.ps1
    & ./scripts/Test-RealContentUiCloseoutV0512Contract.ps1
    & ./scripts/Test-BangumiAccountIntegration.ps1

    Write-Host '== Restore pinned dependencies =='
    & ./scripts/Restore-EizoPlayback.ps1
    & ./scripts/Restore-EizoMetadata.ps1

    Write-Host '== Run cache and Bangumi regressions =='
    dotnet test tests/Eizo.Cache.Tests/Eizo.Cache.Tests.csproj --configuration Release
    Assert-LastExitCode 'Cache integration tests failed'
    dotnet test tests/Eizo.Bangumi.Tests/Eizo.Bangumi.Tests.csproj --configuration Release
    Assert-LastExitCode 'Bangumi integration tests failed'

    Write-Host '== Compile Eizo 1.2.0 =='
    msbuild src\Eizo.App\Eizo.App.csproj /restore /m /p:Configuration=Release /p:Platform=x64 /p:AppxPackageSigningEnabled=false /p:GenerateAppxPackageOnBuild=false
    Assert-LastExitCode 'Release build failed'

    Write-Host 'Eizo 1.2.0 / 1.2.0.0 unified card UI acceptance PASS.'
}
finally {
    Pop-Location
}
