$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$patchPath = Join-Path $repoRoot 'eng/playback-patches/0001-playback-concurrency.patch'
$targetPath = 'src/Eizo.Playback.Core/PlaybackTrace.cs'

if (-not (Test-Path -LiteralPath $patchPath -PathType Leaf)) {
    throw "Playback patch was not found: $patchPath"
}

$existing = Get-Content -LiteralPath $patchPath -Raw
if ($existing.Contains("diff --git a/$targetPath b/$targetPath")) {
    Write-Host 'PlaybackTrace patch is already present.'
    exit 0
}

$source = @'
using System.Diagnostics;

namespace Eizo.Playback.Core;

/// <summary>
/// Lightweight diagnostic tracing shared by the playback backend and its UI host.
/// It intentionally has no dependency on an application logging framework.
/// </summary>
public static class PlaybackTrace
{
    public static void Write(
        string component,
        string operation,
        string phase,
        string? detail = null)
    {
        var message =
            $"[{DateTimeOffset.UtcNow:O}] [T{Environment.CurrentManagedThreadId}] " +
            $"{component}/{operation} {phase}";

        if (!string.IsNullOrWhiteSpace(detail))
        {
            message += " | " + detail;
        }

        Trace.WriteLine(message, "Eizo.Playback");
    }
}
'@

$normalizedSource = $source.Replace("`r`n", "`n").TrimEnd("`n")
$lines = $normalizedSource.Split("`n")
$header = @(
    "diff --git a/$targetPath b/$targetPath",
    'new file mode 100644',
    '--- /dev/null',
    "+++ b/$targetPath",
    "@@ -0,0 +1,$($lines.Count) @@"
)
$body = $lines | ForEach-Object { '+' + $_ }
$addition = "`n" + (($header + $body) -join "`n") + "`n"

[IO.File]::AppendAllText($patchPath, $addition, [Text.UTF8Encoding]::new($false))

$updated = Get-Content -LiteralPath $patchPath -Raw
if (-not $updated.Contains("diff --git a/$targetPath b/$targetPath")) {
    throw 'PlaybackTrace patch append verification failed.'
}

Write-Host "Appended PlaybackTrace.cs to $patchPath ($($lines.Count) lines)."
