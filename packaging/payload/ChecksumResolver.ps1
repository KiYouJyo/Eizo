function Get-ValidSha256Digest([object]$Digest) {
    $text = [string]$Digest
    if ($text -match '^(?i:sha256):(?<hash>[A-Fa-f0-9]{64})$') {
        return $matches.hash.ToUpperInvariant()
    }
    return $null
}

function Resolve-Sha256ManifestHash([string]$ManifestPath, [string]$ExpectedFileName) {
    foreach ($line in Get-Content -LiteralPath $ManifestPath -ErrorAction Stop) {
        $normalized = $line.Trim([char]0xFEFF).Trim()
        if ($normalized -match '^(?<hash>[A-Fa-f0-9]{64})\s+\*?(?<name>.+?)\s*$') {
            $name = $matches.name.Trim()
            if ([string]::Equals($name, $ExpectedFileName, [StringComparison]::Ordinal)) {
                return $matches.hash.ToUpperInvariant()
            }
        }
    }
    return $null
}
