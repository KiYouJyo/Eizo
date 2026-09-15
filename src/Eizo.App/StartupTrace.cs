using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Eizo;

/// <summary>
/// Low-overhead startup timing probe. It records timestamps in memory only during
/// startup and writes the trace on a background thread after the first compositor
/// frame. This is diagnostic-only and does not alter startup sequencing.
/// </summary>
internal static class StartupTrace
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly ConcurrentQueue<string> Entries = new();
    private static int _flushScheduled;

    internal static void Mark(string stage)
    {
        Entries.Enqueue(string.Create(
            CultureInfo.InvariantCulture,
            $"{Clock.Elapsed.TotalMilliseconds,10:0.000} ms | T{Environment.CurrentManagedThreadId,2} | {stage}"));
    }

    internal static void FlushSoon()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) != 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Eizo",
                    "Logs");
                Directory.CreateDirectory(directory);

                var header = new[]
                {
                    $"UTC: {DateTimeOffset.UtcNow:O}",
                    $"Process: {Environment.ProcessId}",
                    $"OS: {Environment.OSVersion}",
                    $"Framework: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
                    string.Empty
                };

                File.WriteAllLines(
                    Path.Combine(directory, "startup-trace.log"),
                    header.Concat(Entries.ToArray()));
            }
            catch
            {
                // Diagnostics must never affect app startup.
            }
        });
    }
}
