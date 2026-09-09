param()

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:RELEASE_CERTIFICATE_BASE64) -or [string]::IsNullOrWhiteSpace($env:RELEASE_CERTIFICATE_PASSWORD)) {
    throw 'GitHub release signing secrets are not configured.'
}

[xml]$manifest = Get-Content -LiteralPath 'src/Eizo.App/Package.appxmanifest' -Raw
$packageVersion = [string]$manifest.Package.Identity.Version
if ($packageVersion -ne '0.3.0.1') {
    throw "Unexpected package version: $packageVersion"
}

Write-Host 'Running v0.3.0 WebDAV runtime probe...'
New-Item -ItemType Directory -Force '.packages/Eizo.Playback' | Out-Null
dotnet run --project tools/Eizo.WebDavV030Probe/Eizo.WebDavV030Probe.csproj --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "WebDAV runtime probe failed: $LASTEXITCODE"
}

Write-Host 'Restoring pinned Eizo.Playback packages...'
& ./scripts/Restore-EizoPlayback.ps1 -Force
if ($LASTEXITCODE -ne 0) {
    throw "Eizo.Playback restore failed: $LASTEXITCODE"
}

Write-Host 'Running authenticated Eizo.Playback -> LibVLC probe...'
dotnet run --project tools/Eizo.AuthenticatedPlaybackProbe/Eizo.AuthenticatedPlaybackProbe.csproj --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Authenticated playback probe failed: $LASTEXITCODE"
}

$bundleOutput = Join-Path $env:RUNNER_TEMP 'Eizo-v0.3.0-AppPackages'
Remove-Item $bundleOutput -Recurse -Force -ErrorAction SilentlyContinue

msbuild src\Eizo.App\Eizo.App.csproj /restore /m /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true /p:AppxPackageSigningEnabled=false /p:AppxBundle=Always /p:AppxBundlePlatforms=x64 /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxPackageDir="$bundleOutput\"
if ($LASTEXITCODE -ne 0) {
    throw "Signed acceptance bundle build failed: $LASTEXITCODE"
}

$unsignedBundle = @(Get-ChildItem $bundleOutput -Recurse -Filter '*.msixbundle' -File) | Select-Object -First 1
if (-not $unsignedBundle) {
    throw 'Acceptance MSIXBundle was not produced.'
}

$artifactDir = Join-Path $env:RUNNER_TEMP 'Eizo-v0.3.0.1-WebDAV-auth-acceptance'
Remove-Item $artifactDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$signedBundle = Join-Path $artifactDir 'Eizo_0.3.0.1_x64.msixbundle'
Copy-Item -LiteralPath $unsignedBundle.FullName -Destination $signedBundle -Force

$pfx = Join-Path $env:RUNNER_TEMP 'eizo-release-signing.pfx'
[IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:RELEASE_CERTIFICATE_BASE64))

$password = ConvertTo-SecureString $env:RELEASE_CERTIFICATE_PASSWORD -AsPlainText -Force
$certificate = Import-PfxCertificate -FilePath $pfx -CertStoreLocation Cert:\CurrentUser\My -Password $password

if ($certificate.Subject -cne 'CN=AppPublisher' -or $certificate.Thumbprint -cne 'BD85AD77A651C86CA01A480C8E9BC64952993F98' -or -not $certificate.HasPrivateKey) {
    throw "Unexpected signing certificate: Subject=$($certificate.Subject); Thumbprint=$($certificate.Thumbprint)"
}

$cer = Join-Path $artifactDir 'Eizo-v0.3.0-AppPublisher.cer'
Export-Certificate -Cert $certificate -FilePath $cer | Out-Null

# The production sideload path trusts the publisher certificate before the app
# can be installed. WinVerifyTrust must run under the same trust conditions.
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

$kitsRoot = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
$signtool = Get-ChildItem (Join-Path $kitsRoot 'Windows Kits\10\bin') -Recurse -Filter signtool.exe | Where-Object FullName -match '\\x64\\signtool.exe$' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) {
    throw 'x64 signtool.exe was not found.'
}

& $signtool.FullName sign /fd SHA256 /sha1 $certificate.Thumbprint /tr http://timestamp.digicert.com /td SHA256 $signedBundle
if ($LASTEXITCODE -ne 0) {
    throw "signtool sign failed: $LASTEXITCODE"
}

$signature = Get-AuthenticodeSignature -FilePath $signedBundle
if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Subject -cne 'CN=AppPublisher' -or $signature.SignerCertificate.Thumbprint -cne $certificate.Thumbprint) {
    throw 'Signed MSIXBundle signer verification failed.'
}
Write-Host 'Authenticode signer PASS.'

$postDownloadBundle = Join-Path $env:RUNNER_TEMP 'Eizo-downloaded-update.msixbundle'
Copy-Item -LiteralPath $signedBundle -Destination $postDownloadBundle -Force

for ($attempt = 1; $attempt -le 2; $attempt++) {
    Write-Host "Updater signature verifier attempt $attempt..."
    dotnet run --project tools/Eizo.UpdateVerifierProbe/Eizo.UpdateVerifierProbe.csproj --configuration Release -- $postDownloadBundle
    if ($LASTEXITCODE -ne 0) {
        throw "Updater signature verifier probe failed on attempt $attempt. Exit=$LASTEXITCODE"
    }

    $probeStream = [IO.File]::Open($postDownloadBundle, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $probeStream.Dispose()
}
Write-Host 'Updater post-download verifier + handle-release smoke PASS.'

$runtimeInstaller = Join-Path $env:RUNNER_TEMP 'WindowsAppRuntimeInstall-x64.exe'
Invoke-WebRequest -Uri 'https://aka.ms/windowsappsdk/1.8/1.8.260710003/windowsappruntimeinstall-x64.exe' -OutFile $runtimeInstaller -UseBasicParsing

$runtimeSignature = Get-AuthenticodeSignature -FilePath $runtimeInstaller
if (-not $runtimeSignature.SignerCertificate -or $runtimeSignature.Status -ne 'Valid' -or $runtimeSignature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
    throw 'Windows App Runtime installer signature validation failed.'
}

Get-AppxPackage -Name Eizo -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue

& $runtimeInstaller --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Windows App Runtime installer failed: $LASTEXITCODE"
}

Add-AppxPackage -Path $signedBundle -ForceApplicationShutdown
$pkg = Get-AppxPackage -Name Eizo
if (-not $pkg -or [string]$pkg.Version -ne '0.3.0.1') {
    throw 'Eizo v0.3.0 acceptance package was not registered correctly.'
}

$installedManifest = Get-AppxPackageManifest -Package $pkg
$appId = [string]$installedManifest.Package.Applications.Application.Id
$activation = "shell:AppsFolder\$($pkg.PackageFamilyName)!$appId"
Start-Process explorer.exe -ArgumentList $activation

$installRoot = [IO.Path]::GetFullPath($pkg.InstallLocation)
$deadline = (Get-Date).AddSeconds(25)
$windowProcess = $null
do {
    Start-Sleep -Milliseconds 500
    $candidates = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.Path -and ([IO.Path]::GetFullPath($_.Path)).StartsWith($installRoot, [StringComparison]::OrdinalIgnoreCase)
        }
        catch { $false }
    })

    foreach ($candidate in $candidates) {
        $candidate.Refresh()
        if (-not $candidate.HasExited -and $candidate.Responding -and $candidate.MainWindowHandle -ne [IntPtr]::Zero) {
            $windowProcess = $candidate
            break
        }
    }
}
while (-not $windowProcess -and (Get-Date) -lt $deadline)

if (-not $windowProcess) {
    throw 'Eizo did not create a responsive visible window.'
}
Write-Host "Visible-window smoke PASS. PID=$($windowProcess.Id)"

Start-Sleep -Seconds 15
$survivor = Get-Process -Id $windowProcess.Id -ErrorAction SilentlyContinue
if (-not $survivor -or -not $survivor.Responding -or $survivor.MainWindowHandle -eq [IntPtr]::Zero) {
    throw 'Eizo did not remain responsive for the sustained smoke interval.'
}
Write-Host "Sustained-window smoke PASS. PID=$($survivor.Id)"

$survivor | Stop-Process -Force -ErrorAction SilentlyContinue
Get-AppxPackage -Name Eizo | Remove-AppxPackage -ErrorAction SilentlyContinue

$shaFile = Join-Path $artifactDir 'SHA256SUMS.txt'
$manifestLines = foreach ($file in @($signedBundle, $cer)) {
    $item = Get-Item -LiteralPath $file
    "{0}  {1}" -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(), $item.Name
}
Set-Content -LiteralPath $shaFile -Value $manifestLines -Encoding ascii
Get-Content -LiteralPath $shaFile

Remove-Item -LiteralPath $pfx -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $postDownloadBundle -Force -ErrorAction SilentlyContinue

"FINAL_ACCEPTANCE_BUNDLE=$signedBundle" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
"FINAL_ACCEPTANCE_CERTIFICATE=$cer" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
"FINAL_ACCEPTANCE_SHA256=$shaFile" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append

Write-Host "Eizo v0.3.0.1 WebDAV authenticated-playback acceptance package ready: $signedBundle"
