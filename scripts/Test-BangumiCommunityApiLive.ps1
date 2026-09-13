param(
    [int] $SubjectId = 8
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Net.Http

$client = [System.Net.Http.HttpClient]::new()
$client.BaseAddress = [Uri]'https://next.bgm.tv/'
$client.Timeout = [TimeSpan]::FromSeconds(20)
$null = $client.DefaultRequestHeaders.TryAddWithoutValidation(
    'User-Agent',
    'KiYouJyo/Eizo/0.4.5 (Windows) (https://github.com/KiYouJyo/Eizo)')
$null = $client.DefaultRequestHeaders.TryAddWithoutValidation(
    'Accept',
    'application/json')

try {
    $resources = @(
        @{ Name = 'comments'; Limit = 1 },
        @{ Name = 'reviews'; Limit = 1 },
        @{ Name = 'topics'; Limit = 1 },
        @{ Name = 'recs'; Limit = 1 },
        @{ Name = 'relations'; Limit = 1 }
    )

    $counts = @{}

    foreach ($resource in $resources) {
        $relative = "p1/subjects/$SubjectId/$($resource.Name)?limit=$($resource.Limit)&offset=0"
        Write-Host "Probe: https://next.bgm.tv/$relative"

        $response = $client.GetAsync($relative).GetAwaiter().GetResult()
        try {
            $response.EnsureSuccessStatusCode() | Out-Null
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $json = $body | ConvertFrom-Json

            if ($null -eq $json.total -or $null -eq $json.data) {
                throw "Community endpoint did not return paged data: $($resource.Name)"
            }

            $counts[$resource.Name] = [int]$json.total
        }
        finally {
            $response.Dispose()
        }
    }

    Write-Host (
        'Bangumi community API LIVE PASS: ' +
        ($resources | ForEach-Object {
            "$($_.Name)=$($counts[$_.Name])"
        } | Join-String -Separator ', ')
    )
}
finally {
    $client.Dispose()
}
