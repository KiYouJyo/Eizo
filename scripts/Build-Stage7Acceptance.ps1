param()

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:RELEASE_CERTIFICATE_BASE64) -or [string]::IsNullOrWhiteSpace($env:RELEASE_CERTIFICATE_PASSWORD)) { throw 'GitHub release signing secrets are not configured.' }
if ([string]::IsNullOrWhiteSpace($env:PLAYBACK_MSIX) -or -not (Test-Path -LiteralPath $env:PLAYBACK_MSIX -PathType Leaf)) { throw 'PLAYBACK_MSIX is missing.' }

$assets = Join-Path $env:RUNNER_TEMP 'Eizo-v0.2.0-stage7-signed-acceptance'
Remove-Item $assets -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $assets | Out-Null

$signedMsix = Join-Path $assets 'Eizo_0.2.0.0_x64_stage7.msix'
Copy-Item -LiteralPath $env:PLAYBACK_MSIX -Destination $signedMsix -Force

$pfx = Join-Path $env:RUNNER_TEMP 'eizo-release-signing.pfx'
[IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:RELEASE_CERTIFICATE_BASE64))
$password = ConvertTo-SecureString $env:RELEASE_CERTIFICATE_PASSWORD -AsPlainText -Force
$certificate = Import-PfxCertificate -FilePath $pfx -CertStoreLocation Cert:\CurrentUser\My -Password $password

if ($certificate.Subject -cne 'CN=AppPublisher' -or $certificate.Thumbprint -cne 'BD85AD77A651C86CA01A480C8E9BC64952993F98' -or -not $certificate.HasPrivateKey) { throw 'Unexpected signing certificate.' }

$cer = Join-Path $assets 'Eizo-v0.2.0-AppPublisher.cer'
Export-Certificate -Cert $certificate -FilePath $cer | Out-Null

$kitsRoot = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
$signtool = Get-ChildItem (Join-Path $kitsRoot 'Windows Kits\10\bin') -Recurse -Filter signtool.exe | Where-Object FullName -match '\\x64\\signtool.exe$' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) { throw 'x64 signtool.exe was not found.' }

& $signtool.FullName sign /fd SHA256 /sha1 $certificate.Thumbprint /tr http://timestamp.digicert.com /td SHA256 $signedMsix
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed: $LASTEXITCODE" }

$signature = Get-AuthenticodeSignature -FilePath $signedMsix
if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Subject -cne 'CN=AppPublisher' -or $signature.SignerCertificate.Thumbprint -cne $certificate.Thumbprint) { throw 'Signed MSIX signer verification failed.' }

$runtimeInstaller = Join-Path $env:RUNNER_TEMP 'WindowsAppRuntimeInstall-x64.exe'
Invoke-WebRequest -Uri 'https://aka.ms/windowsappsdk/1.8/1.8.260710003/windowsappruntimeinstall-x64.exe' -OutFile $runtimeInstaller -UseBasicParsing
$runtimeSignature = Get-AuthenticodeSignature -FilePath $runtimeInstaller
if (-not $runtimeSignature.SignerCertificate -or $runtimeSignature.Status -ne 'Valid' -or $runtimeSignature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw 'Windows App Runtime installer signature validation failed.' }

$publicCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cer)
$trustedStore = 'Cert:\LocalMachine\TrustedPeople'
$trusted = Get-ChildItem -Path $trustedStore -ErrorAction SilentlyContinue | Where-Object Thumbprint -eq $publicCert.Thumbprint | Select-Object -First 1
if (-not $trusted) { Import-Certificate -FilePath $cer -CertStoreLocation $trustedStore | Out-Null }

Get-AppxPackage -Name Eizo -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
& $runtimeInstaller --quiet
if ($LASTEXITCODE -ne 0) { throw "Windows App Runtime installer failed: $LASTEXITCODE" }

Add-AppxPackage -Path $signedMsix -ForceApplicationShutdown
$pkg = Get-AppxPackage -Name Eizo
if (-not $pkg) { throw 'Eizo package was not registered after installation.' }

$manifest = Get-AppxPackageManifest -Package $pkg
$appId = [string]$manifest.Package.Applications.Application.Id
$activation = "shell:AppsFolder\$($pkg.PackageFamilyName)!$appId"
Start-Process explorer.exe -ArgumentList $activation
Start-Sleep -Seconds 8

$installRoot = [IO.Path]::GetFullPath($pkg.InstallLocation)
$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { try { $_.Path -and ([IO.Path]::GetFullPath($_.Path)).StartsWith($installRoot, [StringComparison]::OrdinalIgnoreCase) } catch { $false } })
if ($running.Count -eq 0) {
    Write-Host 'Eizo process did not remain alive. Capturing diagnostics.'

    $startupLog = Join-Path $env:LOCALAPPDATA 'Eizo\Logs\startup-failure.log'
    if (Test-Path -LiteralPath $startupLog) {
        Write-Host '===== Eizo startup-failure.log ====='
        Get-Content -LiteralPath $startupLog -Raw | Write-Host
        Write-Host '===== end startup-failure.log ====='
    }
    else {
        Write-Host "No startup failure log at $startupLog"
    }

    foreach ($logName in @(
        'Microsoft-Windows-AppModel-Runtime/Admin',
        'Microsoft-Windows-TWinUI/Operational'
    )) {
        Write-Host "===== $logName ====="
        Get-WinEvent -FilterHashtable @{ LogName=$logName; StartTime=(Get-Date).AddMinutes(-3) } -ErrorAction SilentlyContinue |
            Where-Object { $_.Message -match '(?i)Eizo|1z32rh13vfry6' } |
            Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message |
            Format-List | Out-Host
    }

    Write-Host '===== Application ====='
    Get-WinEvent -FilterHashtable @{ LogName='Application'; StartTime=(Get-Date).AddMinutes(-3) } -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProviderName -in @('Application Error','.NET Runtime','Windows Error Reporting','Microsoft-Windows-AppModel-Runtime') -or
            $_.Message -match '(?i)Eizo|Eizo\.App'
        } |
        Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message |
        Format-List | Out-Host

    throw 'Eizo failed the signed Stage 7 launch smoke test.'
}

$running | Stop-Process -Force -ErrorAction SilentlyContinue
Get-AppxPackage -Name Eizo | Remove-AppxPackage -ErrorAction SilentlyContinue

$installText = "Eizo Stage 7 signed acceptance package`r`nVersion: 0.2.0.0`r`nArchitecture: x64`r`nSigner: CN=AppPublisher`r`n`r`n1. Import Eizo-v0.2.0-AppPublisher.cer into Local Computer > Trusted People.`r`n2. Double-click Eizo_0.2.0.0_x64_stage7.msix.`r`n3. Install Microsoft Windows App Runtime 1.8 x64 first if Windows reports that the runtime is missing."
Set-Content -LiteralPath (Join-Path $assets 'INSTALL.txt') -Value $installText -Encoding utf8

$hashFiles = @($signedMsix, $cer)
$lines = foreach ($file in $hashFiles) { $item = Get-Item -LiteralPath $file; "{0}  {1}" -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(), $item.Name }
$shaFile = Join-Path $assets 'SHA256SUMS.txt'
Set-Content -LiteralPath $shaFile -Value $lines -Encoding ascii

$zip = Join-Path $env:RUNNER_TEMP 'Eizo-v0.2.0-stage7-signed-acceptance.zip'
Compress-Archive -Path (Join-Path $assets '*') -DestinationPath $zip -CompressionLevel Optimal -Force
"ACCEPTANCE_ZIP=$zip" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
Remove-Item -LiteralPath $pfx -Force -ErrorAction SilentlyContinue
Write-Host "Acceptance ZIP: $zip"
