[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EntryLogPath,
    [Parameter(Mandatory)][string]$EntryCommandPath,
    [Parameter(Mandatory)][string]$EntryWorkingDirectory,
    [switch]$Elevated
)

$ErrorActionPreference = 'Stop'

function Write-EntryLog([string]$Message) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $EntryLogPath) | Out-Null
    "{0:u} [Uninstall] {1}" -f (Get-Date), $Message |
        Add-Content -LiteralPath $EntryLogPath -Encoding UTF8
}

function Test-IsAdministrator {
    $principal = [Security.Principal.WindowsPrincipal]::new(
        [Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Quote-Argument([string]$Value) {
    if ($Value.Contains('"')) {
        throw 'Windows paths cannot contain a quotation mark.'
    }
    return '"{0}"' -f $Value
}

try {
    $payloadScript = Join-Path $PSScriptRoot 'Uninstall.ps1'
    if (-not (Test-Path -LiteralPath $payloadScript -PathType Leaf)) {
        throw "Missing payload script: $payloadScript"
    }

    if (-not (Test-IsAdministrator)) {
        Write-EntryLog 'Requesting UAC elevation for package/certificate cleanup.'

        $arguments = @(
            '-NoLogo',
            '-NoProfile',
            '-ExecutionPolicy',
            'Bypass',
            '-File',
            (Quote-Argument $PSCommandPath),
            '-EntryLogPath',
            (Quote-Argument $EntryLogPath),
            '-EntryCommandPath',
            (Quote-Argument $EntryCommandPath),
            '-EntryWorkingDirectory',
            (Quote-Argument $EntryWorkingDirectory),
            '-Elevated'
        ) -join ' '

        $child =
            Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Verb RunAs -Wait -PassThru
        exit $child.ExitCode
    }

    Write-EntryLog 'Running Eizo uninstall payload.'
    & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $payloadScript -RemoveCertificate
    exit $LASTEXITCODE
}
catch {
    Write-EntryLog "Launcher failure: $($_.Exception.Message)"
    Write-Error $_
    exit 1
}
