param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$locales = @("zh-CN", "ja-JP", "en-US")
$resourceRoot = Join-Path $RepositoryRoot "src/Eizo.App/Strings"
$resources = @{}

foreach ($locale in $locales) {
    $path = Join-Path $resourceRoot "$locale/Resources.resw"

    if (-not (Test-Path $path)) {
        throw "Missing localization file: $path"
    }

    [xml]$xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $dataNodes = @($xml.root.data)
    $keys = @($dataNodes | ForEach-Object { [string]$_.name })

    $duplicates = @(
        $keys |
            Group-Object |
            Where-Object Count -gt 1 |
            ForEach-Object Name
    )

    if ($duplicates.Count -gt 0) {
        throw "Duplicate resource keys in $($locale): $($duplicates -join ', ')"
    }

    $emptyKeys = @(
        $dataNodes |
            Where-Object { [string]::IsNullOrWhiteSpace([string]$_.value) } |
            ForEach-Object { [string]$_.name }
    )

    if ($emptyKeys.Count -gt 0) {
        throw "Empty resource values in $($locale): $($emptyKeys -join ', ')"
    }

    $resources[$locale] = @($keys | Sort-Object)
}

$baselineLocale = "en-US"
$baseline = $resources[$baselineLocale]

foreach ($locale in $locales | Where-Object { $_ -ne $baselineLocale }) {
    $difference = Compare-Object -ReferenceObject $baseline -DifferenceObject $resources[$locale]

    if ($difference) {
        $missing = @(
            $difference |
                Where-Object SideIndicator -eq "<=" |
                ForEach-Object InputObject
        )
        $extra = @(
            $difference |
                Where-Object SideIndicator -eq "=>" |
                ForEach-Object InputObject
        )

        $message = "Localization key mismatch for $($locale)."
        if ($missing.Count -gt 0) {
            $message += " Missing: $($missing -join ', ')."
        }
        if ($extra.Count -gt 0) {
            $message += " Extra: $($extra -join ', ')."
        }

        throw $message
    }
}

Write-Host "Localization validation passed: $($baseline.Count) keys across $($locales.Count) locales."
