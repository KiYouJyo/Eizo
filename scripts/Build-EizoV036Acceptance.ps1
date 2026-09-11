param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-LastExitCode([string]$message) {
    if ($LASTEXITCODE -ne 0) { throw "$message ($LASTEXITCODE)" }
}

function Get-EizoProcesses([string]$installRoot) {
    $root = [IO.Path]::GetFullPath($installRoot)
    return @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.Path -and ([IO.Path]::GetFullPath($_.Path)).StartsWith($root, [StringComparison]::OrdinalIgnoreCase)
        }
        catch { $false }
    })
}

function Start-EizoAndAssertAlive($package, [int]$waitSeconds = 8) {
    $manifest = Get-AppxPackageManifest -Package $package
    $appId = [string]$manifest.Package.Applications.Application.Id
    $activation = "shell:AppsFolder\$($package.PackageFamilyName)!$appId"
    Start-Process explorer.exe -ArgumentList $activation
    Start-Sleep -Seconds $waitSeconds
    $running = Get-EizoProcesses ([string]$package.InstallLocation)
    if ($running.Count -eq 0) { throw 'Eizo did not remain running after activation.' }
    return $running
}

if ([string]::IsNullOrWhiteSpace($env:RELEASE_CERTIFICATE_BASE64) -or
    [string]::IsNullOrWhiteSpace($env:RELEASE_CERTIFICATE_PASSWORD)) {
    throw 'GitHub release signing secrets are not configured.'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    $runnerTemp = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { [IO.Path]::GetTempPath() } else { $env:RUNNER_TEMP }
    $assets = Join-Path $runnerTemp 'Eizo-v0.3.6-acceptance'
    $appPackages = Join-Path $runnerTemp 'Eizo-AppPackages-v036'
    $bundleExtract = Join-Path $runnerTemp 'Eizo-bundle-extract-v036'
    $oneClickStaging = Join-Path $runnerTemp 'Eizo-one-click-v036'
    foreach ($path in @($assets, $appPackages, $bundleExtract, $oneClickStaging)) {
        if (Test-Path $path) { Remove-Item -LiteralPath $path -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }

    Write-Host '== Restore pinned dependencies =='
    & ./scripts/Restore-EizoPlayback.ps1
    & ./scripts/Restore-EizoMetadata.ps1

    Write-Host '== Compile Release =='
    msbuild src\Eizo.App\Eizo.App.csproj /restore /m /p:Configuration=Release /p:Platform=x64 /p:AppxPackageSigningEnabled=false /p:GenerateAppxPackageOnBuild=false
    Assert-LastExitCode 'Release build failed'

    $releaseOut = (Resolve-Path 'src\Eizo.App\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64').Path
    $playbackAssemblies = @(
        'Eizo.Playback.Abstractions.dll',
        'Eizo.Playback.Core.dll',
        'Eizo.Playback.LibVLC.dll',
        'Eizo.Playback.LibVLC.WinUI.dll'
    )
    $updateableAssemblies = @($playbackAssemblies + 'Eizo.Metadata.Recognition.dll')
    foreach ($name in $updateableAssemblies) {
        if (Test-Path (Join-Path $releaseOut $name)) {
            throw "Updateable implementation remains in the default probing root: $name"
        }
    }
    foreach ($name in $playbackAssemblies) {
        if (-not (Test-Path (Join-Path $releaseOut "Components\Bundled\Playback\$name"))) {
            throw "Bundled Playback fallback is missing: $name"
        }
    }
    if (-not (Test-Path (Join-Path $releaseOut 'Components\Bundled\Recognition\Eizo.Metadata.Recognition.dll'))) {
        throw 'Bundled Metadata fallback is missing.'
    }
    Write-Host 'Relocatable component output contract PASS.'

    Write-Host '== Build MSIX bundle =='
    msbuild src\Eizo.App\Eizo.App.csproj /restore /m /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true /p:AppxPackageSigningEnabled=false /p:AppxBundle=Always /p:AppxBundlePlatforms=x64 /p:UapAppxPackageBuildMode=SideloadOnly "/p:AppxPackageDir=$appPackages\"
    Assert-LastExitCode 'MSIX build failed'
    $producedBundle = @(Get-ChildItem $appPackages -Recurse -Filter '*.msixbundle' -File) | Select-Object -First 1
    if (-not $producedBundle) { throw 'MSIX bundle was not produced.' }
    $bundlePath = Join-Path $assets 'Eizo_0.3.6.0_x64.msixbundle'
    Copy-Item -LiteralPath $producedBundle.FullName -Destination $bundlePath -Force

    Write-Host '== Verify packaged component probing contract =='
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($bundlePath, $bundleExtract)
    $inner = @(Get-ChildItem $bundleExtract -Recurse -Filter '*.msix' -File) | Select-Object -First 1
    if (-not $inner) { throw 'Bundle contains no inner MSIX.' }
    $archive = [System.IO.Compression.ZipFile]::OpenRead($inner.FullName)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
        foreach ($name in @('libvlc.dll', 'libvlccore.dll')) {
            if (-not ($entries | Where-Object { $_ -match "(^|/)$([regex]::Escape($name))$" })) {
                throw "MSIX does not contain $name"
            }
        }
        $plugins = @($entries | Where-Object { $_ -match '(^|/)plugins/.+\.dll$' })
        if ($plugins.Count -eq 0) { throw 'MSIX contains no LibVLC plugins.' }
        foreach ($name in $updateableAssemblies) {
            if ($entries -contains $name) { throw "MSIX root still contains updateable implementation: $name" }
        }
        foreach ($name in $playbackAssemblies) {
            if ($entries -notcontains "Components/Bundled/Playback/$name") {
                throw "MSIX bundled Playback fallback is missing: $name"
            }
        }
        if ($entries -notcontains 'Components/Bundled/Recognition/Eizo.Metadata.Recognition.dll') {
            throw 'MSIX bundled Metadata fallback is missing.'
        }
        Write-Host "Component bundle contract PASS. LibVLC plugins=$($plugins.Count)"
    }
    finally { $archive.Dispose() }

    Write-Host '== Sign bundle =='
    $pfx = Join-Path $runnerTemp 'eizo-v036-release-signing.pfx'
    [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:RELEASE_CERTIFICATE_BASE64))
    $password = ConvertTo-SecureString $env:RELEASE_CERTIFICATE_PASSWORD -AsPlainText -Force
    $certificate = Import-PfxCertificate -FilePath $pfx -CertStoreLocation Cert:\CurrentUser\My -Password $password
    if ($certificate.Subject -cne 'CN=AppPublisher' -or
        $certificate.Thumbprint -cne 'BD85AD77A651C86CA01A480C8E9BC64952993F98' -or
        -not $certificate.HasPrivateKey) {
        throw "Unexpected signing certificate: Subject=$($certificate.Subject); Thumbprint=$($certificate.Thumbprint)"
    }
    $cer = Join-Path $assets 'Eizo-v0.3.6-AppPublisher.cer'
    Export-Certificate -Cert $certificate -FilePath $cer | Out-Null
    $kitsRoot = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $signtool = Get-ChildItem (Join-Path $kitsRoot 'Windows Kits\10\bin') -Recurse -Filter signtool.exe |
        Where-Object FullName -match '\\x64\\signtool.exe$' |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $signtool) { throw 'x64 signtool.exe was not found.' }
    & $signtool.FullName sign /fd SHA256 /sha1 $certificate.Thumbprint /tr http://timestamp.digicert.com /td SHA256 $bundlePath
    Assert-LastExitCode 'signtool sign failed'
    $signature = Get-AuthenticodeSignature -FilePath $bundlePath
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -cne $certificate.Thumbprint) {
        throw 'Signed bundle signer mismatch.'
    }
    Remove-Item -LiteralPath $pfx -Force

    Write-Host '== Install and launch bundled fallback =='
    if (-not (Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
        Where-Object Thumbprint -eq $certificate.Thumbprint | Select-Object -First 1)) {
        Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    }
    Get-AppxPackage -Name Eizo -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
    $legacyComponentsRoot = Join-Path $env:LOCALAPPDATA 'Eizo\Components'
    Remove-Item -LiteralPath $legacyComponentsRoot -Recurse -Force -ErrorAction SilentlyContinue

    $runtimeInstaller = Join-Path $runnerTemp 'WindowsAppRuntimeInstall-x64.exe'
    Invoke-WebRequest -Uri 'https://aka.ms/windowsappsdk/1.8/1.8.260710003/windowsappruntimeinstall-x64.exe' -OutFile $runtimeInstaller -UseBasicParsing
    $runtimeSignature = Get-AuthenticodeSignature -FilePath $runtimeInstaller
    if (-not $runtimeSignature.SignerCertificate -or $runtimeSignature.Status -ne 'Valid' -or
        $runtimeSignature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
        throw 'Windows App Runtime installer signature validation failed.'
    }
    & $runtimeInstaller --quiet
    Assert-LastExitCode 'Windows App Runtime installer failed'

    Add-AppxPackage -Path $bundlePath -ForceApplicationShutdown
    $pkg = Get-AppxPackage -Name Eizo
    if (-not $pkg -or [string]$pkg.Version -ne '0.3.6.0') {
        throw "Installed package version mismatch: $($pkg.Version)"
    }

    $packageLocalState = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\LocalState"
    $componentsRoot = Join-Path $packageLocalState 'Eizo\Components'
    Remove-Item -LiteralPath $componentsRoot -Recurse -Force -ErrorAction SilentlyContinue

    $running = Start-EizoAndAssertAlive $pkg
    $bundledRuntimeLog = Join-Path $componentsRoot 'recognition-runtime.log'
    if (-not (Test-Path $bundledRuntimeLog)) {
        throw "Bundled Recognition runtime probe log was not written at $bundledRuntimeLog"
    }
    $bundledRuntimeLine = @(Get-Content -LiteralPath $bundledRuntimeLog | Where-Object { $_ -match '\tversion=' }) | Select-Object -Last 1
    $bundledAssembly = Join-Path ([string]$pkg.InstallLocation) 'Components\Bundled\Recognition\Eizo.Metadata.Recognition.dll'
    if ($bundledRuntimeLine -notmatch '\tversion=0\.1\.0\texternal=False\tprobe=' -or
        $bundledRuntimeLine -notmatch ([regex]::Escape($bundledAssembly))) {
        throw "Bundled Recognition runtime was not actually invoked from the packaged fallback. Log:`n$bundledRuntimeLine"
    }
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host 'Bundled fallback launch and real Recognition call PASS.'

    Write-Host '== Stage published Metadata v0.1.1 and simulate restart =='
    $metadataRoot = Join-Path $componentsRoot 'Recognition'
    $metadataVersionRoot = Join-Path $metadataRoot 'versions\0.1.1'
    Remove-Item -LiteralPath $metadataRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $metadataVersionRoot | Out-Null
    $metadataZip = Join-Path $runnerTemp 'Eizo.Recognition.Runtime-v0.1.1-x64.zip'
    Invoke-WebRequest -Uri 'https://github.com/KiYouJyo/Eizo.Metadata/releases/download/v0.1.1/Eizo.Recognition.Runtime-v0.1.1-x64.zip' -OutFile $metadataZip -UseBasicParsing
    $metadataExpected = 'c29a5466a9a9b92b8d04b04bc16709bd11827124338cfebf76e4a205fa0263cf'
    $metadataActual = (Get-FileHash -LiteralPath $metadataZip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($metadataActual -ne $metadataExpected) { throw "Published Metadata v0.1.1 digest mismatch: $metadataActual" }
    Expand-Archive -LiteralPath $metadataZip -DestinationPath $metadataVersionRoot -Force
    if (-not (Test-Path (Join-Path $metadataVersionRoot 'eizo-recognition-release.json'))) { throw 'Metadata manifest is missing.' }
    if (-not (Test-Path (Join-Path $metadataVersionRoot 'bin\Eizo.Metadata.Recognition.dll'))) { throw 'Metadata Recognition module is missing.' }
    '{"Version":"0.1.1"}' | Set-Content -LiteralPath (Join-Path $metadataRoot 'pending.json') -Encoding utf8NoBOM

    $running = Start-EizoAndAssertAlive $pkg
    $activePath = Join-Path $metadataRoot 'active.json'
    $pendingPath = Join-Path $metadataRoot 'pending.json'
    if (-not (Test-Path $activePath)) { throw 'Metadata active.json was not promoted on restart.' }
    $active = Get-Content -LiteralPath $activePath -Raw | ConvertFrom-Json
    if ([string]$active.Version -ne '0.1.1') { throw "Unexpected active Metadata version: $($active.Version)" }
    if (Test-Path $pendingPath) { throw 'Metadata pending.json still exists after activation.' }
    $activationLog = Join-Path $componentsRoot 'activation.log'
    if (-not (Test-Path $activationLog)) { throw 'Component activation diagnostics log was not written.' }
    $log = Get-Content -LiteralPath $activationLog -Raw
    if ($log -notmatch 'Metadata\tcurrent=0\.1\.1\tbundled=0\.1\.0\texternal=True') {
        throw "Metadata external activation was not recorded. Log:`n$log"
    }

    $runtimeLog = Join-Path $componentsRoot 'recognition-runtime.log'
    if (-not (Test-Path $runtimeLog)) { throw 'Recognition runtime probe log was not written.' }
    $runtimeLine = @(Get-Content -LiteralPath $runtimeLog | Where-Object { $_ -match '\tversion=' }) | Select-Object -Last 1
    $externalAssembly = Join-Path $metadataVersionRoot 'bin\Eizo.Metadata.Recognition.dll'
    if ($runtimeLine -notmatch '\tversion=0\.1\.1\texternal=True\tprobe=' -or
        $runtimeLine -notmatch ([regex]::Escape($externalAssembly))) {
        throw "Metadata v0.1.1 state was promoted but the external Recognition runtime was not actually invoked. Log:`n$runtimeLine"
    }

    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host 'Metadata v0.1.1 restart activation and real Recognition call PASS.'

    Write-Host '== Build one-click acceptance assets =='
    Get-AppxPackage -Name Eizo -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $componentsRoot -Recurse -Force -ErrorAction SilentlyContinue
    & ./packaging/New-GitHubOneClickInstallerPackage.ps1 -SignedBundlePath $bundlePath -PublicCertificatePath $cer -OutputDirectory $oneClickStaging -DisplayVersion '0.3.6' -PackageVersion '0.3.6.0'
    $packageRoot = Join-Path $oneClickStaging 'Eizo-v0.3.6-x64-one-click'
    & ./packaging/Test-GitHubOneClickInstallerPackage.ps1 -ReleaseDirectory $packageRoot
    $oneClickZip = Join-Path $assets 'Eizo-v0.3.6-x64-one-click.zip'
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $oneClickZip -CompressionLevel Optimal

    $sumLines = foreach ($file in @($bundlePath, $oneClickZip)) {
        $item = Get-Item -LiteralPath $file
        "{0}  {1}" -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(), $item.Name
    }
    $sumPath = Join-Path $assets 'SHA256SUMS.txt'
    Set-Content -LiteralPath $sumPath -Value $sumLines -Encoding ascii
    Get-Content -LiteralPath $sumPath
    Write-Host "Eizo 0.3.6 acceptance PASS. Assets=$assets"
}
finally {
    Pop-Location
}
