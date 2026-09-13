param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$manifest = [IO.File]::ReadAllText((Join-Path $root 'src/Eizo.App/Package.appxmanifest'))
$app = [IO.File]::ReadAllText((Join-Path $root 'src/Eizo.App/App.xaml.cs'))
$store = [IO.File]::ReadAllText((Join-Path $root 'src/Eizo.App/Models/BangumiAccountCredentialStore.cs'))
$service = [IO.File]::ReadAllText((Join-Path $root 'src/Eizo.App/Models/BangumiOAuthService.cs'))
$worker = [IO.File]::ReadAllText((Join-Path $root 'cloudflare/eizo-bangumi-auth/worker.js'))
foreach ($item in @(
    @($manifest, '<uap:Protocol Name="eizo">'),
    @($app, 'OnInitialActivation'),
    @($app, 'OnRedirectedActivation'),
    @($store, 'PasswordVault'),
    @($service, 'ConnectOAuthAsync'),
    @($worker, 'reserveCallback'),
    @($worker, 'storage.transaction')
)) {
    if (-not $item[0].Contains($item[1], [StringComparison]::Ordinal)) {
        throw "Bangumi OAuth integration missing: $($item[1])"
    }
}
& node --test (Join-Path $root 'cloudflare/eizo-bangumi-auth/worker.test.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Bangumi OAuth Worker tests failed.' }
Write-Host 'Eizo v0.4.4 Bangumi OAuth integration PASS.'
