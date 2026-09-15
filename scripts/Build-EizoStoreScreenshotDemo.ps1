param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-LastExitCode([string]$message) {
    if ($LASTEXITCODE -ne 0) { throw "$message ($LASTEXITCODE)" }
}

if ([string]::IsNullOrWhiteSpace($env:RELEASE_CERTIFICATE_BASE64) -or
    [string]::IsNullOrWhiteSpace($env:RELEASE_CERTIFICATE_PASSWORD)) {
    throw 'GitHub release signing secrets are not configured.'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $repoRoot 'src/Eizo.App/Package.appxmanifest'
$originalManifest = [IO.File]::ReadAllText($manifestPath, [Text.Encoding]::UTF8)

Push-Location $repoRoot
try {
    $runnerTemp = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { [IO.Path]::GetTempPath() } else { $env:RUNNER_TEMP }
    $assets = Join-Path $runnerTemp 'Eizo-Demo-v1.0.0'
    $appPackages = Join-Path $runnerTemp 'Eizo-Demo-AppPackages'
    New-Item -ItemType Directory -Force -Path $assets, $appPackages | Out-Null

    $demoManifest = $originalManifest
        .Replace('<Identity Name="Eizo" Publisher="CN=AppPublisher" Version="1.0.0.0" />',
                 '<Identity Name="Eizo.Demo" Publisher="CN=AppPublisher" Version="1.0.0.0" />')
        .Replace('<DisplayName>Eizo</DisplayName>', '<DisplayName>Eizo Demo</DisplayName>')
        .Replace('DisplayName="Eizo"', 'DisplayName="Eizo Demo"')
        .Replace('<uap:Protocol Name="eizo">', '<uap:Protocol Name="eizo-demo">')
        .Replace('<uap:DisplayName>Eizo Bangumi sign-in</uap:DisplayName>',
                 '<uap:DisplayName>Eizo Demo Bangumi sign-in</uap:DisplayName>')

    if ($demoManifest -eq $originalManifest) {
        throw 'Demo manifest transformation did not change the source manifest.'
    }

    [IO.File]::WriteAllText($manifestPath, $demoManifest, [Text.UTF8Encoding]::new($false))

    [xml]$verifyManifest = Get-Content -LiteralPath $manifestPath -Raw
    if ([string]$verifyManifest.Package.Identity.Name -cne 'Eizo.Demo' -or
        [string]$verifyManifest.Package.Properties.DisplayName -cne 'Eizo Demo') {
        throw 'Demo package identity injection failed.'
    }

    & ./scripts/Restore-EizoPlayback.ps1
    & ./scripts/Restore-EizoMetadata.ps1

    $args = @(
        'src\Eizo.App\Eizo.App.csproj',
        '/restore',
        '/m',
        '/p:Configuration=Release',
        '/p:Platform=x64',
        '/p:EizoDemo=true',
        '/p:GenerateAppxPackageOnBuild=true',
        '/p:AppxPackageSigningEnabled=false',
        '/p:AppxBundle=Always',
        '/p:AppxBundlePlatforms=x64',
        '/p:UapAppxPackageBuildMode=SideloadOnly',
        "/p:AppxPackageDir=$appPackages\"
    )
    & msbuild @args
    Assert-LastExitCode 'Eizo Demo MSIX build failed'

    $bundle = @(Get-ChildItem $appPackages -Recurse -Filter '*.msixbundle' -File | Sort-Object Length -Descending) | Select-Object -First 1
    if (-not $bundle) { throw 'Eizo Demo MSIXBundle was not produced.' }

    $bundlePath = Join-Path $assets 'Eizo-Demo_1.0.0.0_x64.msixbundle'
    Copy-Item -LiteralPath $bundle.FullName -Destination $bundlePath -Force

    $pfx = Join-Path $runnerTemp 'eizo-demo-signing.pfx'
    [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:RELEASE_CERTIFICATE_BASE64))
    $password = ConvertTo-SecureString $env:RELEASE_CERTIFICATE_PASSWORD -AsPlainText -Force
    $certificate = Import-PfxCertificate -FilePath $pfx -CertStoreLocation Cert:\CurrentUser\My -Password $password

    if ($certificate.Subject -cne 'CN=AppPublisher' -or -not $certificate.HasPrivateKey) {
        throw "Unexpected signing certificate: $($certificate.Subject)"
    }

    $kitsRoot = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $signtool = Get-ChildItem (Join-Path $kitsRoot 'Windows Kits\10\bin') -Recurse -Filter signtool.exe |
        Where-Object FullName -match '\\x64\\signtool.exe$' |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $signtool) { throw 'x64 signtool.exe was not found.' }

    & $signtool.FullName sign /fd SHA256 /sha1 $certificate.Thumbprint /tr http://timestamp.digicert.com /td SHA256 $bundlePath
    Assert-LastExitCode 'Eizo Demo signing failed'

    $cer = Join-Path $assets 'Eizo-Demo-AppPublisher.cer'
    Export-Certificate -Cert $certificate -FilePath $cer | Out-Null
    Remove-Item -LiteralPath $pfx -Force

    $sum = "{0}  {1}" -f (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $bundlePath)
    Set-Content -LiteralPath (Join-Path $assets 'SHA256SUMS.txt') -Value $sum -Encoding ascii

    @(
        'Eizo Store Screenshot Demo'
        'Display name: Eizo Demo'
        'Package identity: Eizo.Demo'
        'Executable flavor: Eizo.Demo'
        'Single-instance key: Eizo.Demo.Main'
        'Protocol: eizo-demo://'
        'Purpose: promotional screenshots only'
    ) | Set-Content -LiteralPath (Join-Path $assets 'DEMO-IDENTITY.txt') -Encoding utf8

    Write-Host "Eizo Demo package PASS. Assets=$assets"
}
finally {
    [IO.File]::WriteAllText($manifestPath, $originalManifest, [Text.UTF8Encoding]::new($false))
    Pop-Location
}
