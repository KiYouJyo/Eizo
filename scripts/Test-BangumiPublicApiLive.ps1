param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$headers = @{
    'User-Agent' = 'KiYouJyo/Eizo/0.4.2 (Windows) (https://github.com/KiYouJyo/Eizo)'
    'Accept' = 'application/json'
}

$now = Get-Date
$seasonMonth = ([math]::Floor(($now.Month - 1) / 3) * 3) + 1
$seasonUri = "https://api.bgm.tv/v0/subjects?type=2&cat=1&sort=date&year=$($now.Year)&month=$seasonMonth&limit=5&offset=0"
$rankUri = 'https://api.bgm.tv/v0/subjects?type=2&sort=rank&limit=5&offset=0'
$calendarUri = 'https://api.bgm.tv/calendar'

Write-Host "Season probe: $seasonUri"
$season = Invoke-RestMethod -Uri $seasonUri -Headers $headers -Method Get -TimeoutSec 20
if (-not $season.data -or @($season.data).Count -eq 0) {
    throw "Bangumi season endpoint returned no anime for $($now.Year)-$seasonMonth."
}
if (@($season.data).Count -gt 5) {
    throw 'Bangumi season endpoint ignored requested limit.'
}

Write-Host "Ranking probe: $rankUri"
$ranking = Invoke-RestMethod -Uri $rankUri -Headers $headers -Method Get -TimeoutSec 20
if (-not $ranking.data -or @($ranking.data).Count -eq 0) {
    throw 'Bangumi ranking endpoint returned no anime.'
}

Write-Host "Calendar probe: $calendarUri"
$calendar = Invoke-RestMethod -Uri $calendarUri -Headers $headers -Method Get -TimeoutSec 20
if (-not $calendar -or @($calendar).Count -lt 7) {
    throw 'Bangumi calendar endpoint did not return a full week.'
}

$subjectId = [int]$season.data[0].id
$detailUri = "https://api.bgm.tv/v0/subjects/$subjectId"
Write-Host "Subject detail probe: $detailUri"
$detail = Invoke-RestMethod -Uri $detailUri -Headers $headers -Method Get -TimeoutSec 20
if ([int]$detail.id -ne $subjectId -or [string]::IsNullOrWhiteSpace([string]$detail.name)) {
    throw 'Bangumi subject detail response is invalid.'
}

Write-Host "Bangumi public API LIVE PASS: season=$(@($season.data).Count), ranking=$(@($ranking.data).Count), calendar=$(@($calendar).Count), detail=$subjectId"
