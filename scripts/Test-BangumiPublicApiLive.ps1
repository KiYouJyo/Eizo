param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$userAgent = 'KiYouJyo/Eizo/0.4.2 (Windows) (https://github.com/KiYouJyo/Eizo)'
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(20)
$null = $client.DefaultRequestHeaders.TryAddWithoutValidation('User-Agent', $userAgent)
$null = $client.DefaultRequestHeaders.TryAddWithoutValidation('Accept', 'application/json')

function Invoke-BangumiJson([string] $uri) {
    $response = $client.GetAsync($uri).GetAwaiter().GetResult()
    try {
        if (-not $response.IsSuccessStatusCode) {
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            throw "Bangumi request failed: HTTP $([int]$response.StatusCode) $($response.ReasonPhrase) $body"
        }

        $json = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return $json | ConvertFrom-Json
    }
    finally {
        $response.Dispose()
    }
}

$now = Get-Date
$seasonMonth = ([math]::Floor(($now.Month - 1) / 3) * 3) + 1
$seasonUri = "https://api.bgm.tv/v0/subjects?type=2&cat=1&sort=date&year=$($now.Year)&month=$seasonMonth&limit=5&offset=0"
$rankUri = 'https://api.bgm.tv/v0/subjects?type=2&sort=rank&limit=5&offset=0'
$calendarUri = 'https://api.bgm.tv/calendar'

Write-Host "Season probe: $seasonUri"
$season = Invoke-BangumiJson $seasonUri
if (-not $season.data -or @($season.data).Count -eq 0) {
    throw "Bangumi season endpoint returned no anime for $($now.Year)-$seasonMonth."
}
if (@($season.data).Count -gt 5) {
    throw 'Bangumi season endpoint ignored requested limit.'
}

Write-Host "Ranking probe: $rankUri"
$ranking = Invoke-BangumiJson $rankUri
if (-not $ranking.data -or @($ranking.data).Count -eq 0) {
    throw 'Bangumi ranking endpoint returned no anime.'
}

Write-Host "Calendar probe: $calendarUri"
$calendar = Invoke-BangumiJson $calendarUri
if (-not $calendar -or @($calendar).Count -lt 7) {
    throw 'Bangumi calendar endpoint did not return a full week.'
}

$subjectId = [int]$season.data[0].id
$detailUri = "https://api.bgm.tv/v0/subjects/$subjectId"
Write-Host "Subject detail probe: $detailUri"
$detail = Invoke-BangumiJson $detailUri
if ([int]$detail.id -ne $subjectId -or [string]::IsNullOrWhiteSpace([string]$detail.name)) {
    throw 'Bangumi subject detail response is invalid.'
}

Write-Host "Bangumi public API LIVE PASS: season=$(@($season.data).Count), ranking=$(@($ranking.data).Count), calendar=$(@($calendar).Count), detail=$subjectId"
$client.Dispose()
