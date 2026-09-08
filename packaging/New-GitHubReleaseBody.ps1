[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$tag = "v$Version"

$relativeFiles = @(
    "docs/RELEASE-NOTES-v$Version.md",
    "docs/RELEASE-NOTES-v$Version.ja.md",
    "docs/RELEASE-NOTES-v$Version.en.md"
)

foreach ($relativeFile in $relativeFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $relativeFile) -PathType Leaf)) {
        throw "Required Release Notes sibling is missing: $relativeFile"
    }
}

$canonicalPath = Join-Path $repositoryRoot $relativeFiles[0]
$canonicalLines =
    [System.IO.File]::ReadAllLines($canonicalPath, [System.Text.Encoding]::UTF8)

if ($canonicalLines.Count -lt 2) {
    throw 'Canonical Release Notes are incomplete.'
}

$labels = @(
    [regex]::Matches($canonicalLines[0], '\[(?<label>[^\]]+)\]') |
        ForEach-Object { $_.Groups['label'].Value }
)

$firstSeparator = $canonicalLines[0].IndexOf(' | ', [StringComparison]::Ordinal)
if ($firstSeparator -lt 1 -or $labels.Count -ne 2) {
    throw 'Canonical Release Notes do not have the required language switcher.'
}

$zhLabel = $canonicalLines[0].Substring(0, $firstSeparator)
$repositoryUrl = 'https://github.com/KiYouJyo/Eizo'
$zhUrl = "$repositoryUrl/blob/$tag/$($relativeFiles[0])"
$jaUrl = "$repositoryUrl/blob/$tag/$($relativeFiles[1])"
$enUrl = "$repositoryUrl/blob/$tag/$($relativeFiles[2])"

$published =
    "[$zhLabel]($zhUrl) | [$($labels[0])]($jaUrl) | [$($labels[1])]($enUrl)" +
    [Environment]::NewLine +
    ($canonicalLines[1..($canonicalLines.Count - 1)] -join [Environment]::NewLine)

foreach ($url in @($zhUrl, $jaUrl, $enUrl)) {
    if (-not $published.Contains($url)) {
        throw "Published Release body is missing tag-pinned URL: $url"
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

[System.IO.File]::WriteAllText(
    $OutputPath,
    $published,
    [System.Text.UTF8Encoding]::new($false))
