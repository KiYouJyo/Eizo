using System.Diagnostics;
using System.Text.Json;
using Eizo.Playback;
using Eizo.Playback.Backends.LibVLC;
using Eizo.Playback.Core;

if (args.Length < 1) throw new ArgumentException("Usage: Eizo.PlaybackStress <two-subtitle-video.mkv> [seconds=600] [report.json]");
var source = PlaybackSource.FromFile(Path.GetFullPath(args[0]));
var seconds = args.Length > 1 ? int.Parse(args[1]) : 600;
var report = args.Length > 2 ? args[2] : "playback-stress.json";
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(seconds + 120));
var token = deadline.Token;
var timer = Stopwatch.StartNew();
var samples = new List<double>();
var counts = new Dictionary<string, int>();
var failures = new List<string>();
var cancelled = 0;
var events = 0;
var cycles = 0;
LibVlcPlaybackEngine? engine = null;

async Task Measure(string operation, Func<ValueTask> action)
{
    var watch = Stopwatch.StartNew();
    await action().AsTask().WaitAsync(TimeSpan.FromSeconds(10), token);
    samples.Add(watch.Elapsed.TotalMilliseconds);
    counts[operation] = counts.GetValueOrDefault(operation) + 1;
}

async Task WaitUntil(Func<bool> predicate, Func<ValueTask> refresh)
{
    using var poll = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
    using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
    limit.CancelAfter(TimeSpan.FromSeconds(10));
    while (!predicate())
    {
        await refresh();
        await poll.WaitForNextTickAsync(limit.Token);
    }
}

async Task Open()
{
    engine = await Task.Run(() => new LibVlcPlaybackEngine(new LibVlcPlaybackOptions
    {
        Arguments = ["--aout=dummy", "--vout=dummy", "--intf=dummy", "--no-video-title-show", "--quiet"]
    }), token);
    engine.StateChanged += (_, _) => Interlocked.Increment(ref events);
    engine.Tracks.TracksChanged += (_, _) => Interlocked.Increment(ref events);
    engine.Failed += (_, e) => { lock (failures) failures.Add(e.Error.Code.ToString()); };
    await Measure("open", () => engine.OpenAsync(source, token));
    await Measure("play", () => engine.PlayAsync(token));
    await WaitUntil(() => engine.Tracks.SubtitleTracks.Count >= 2 && engine.Diagnostics.Current.Capabilities.CanSeek,
        async () => { await engine.Tracks.RefreshAsync(token); await engine.Diagnostics.RefreshAsync(token); });
}

try
{
    await Open();
    // Actual native calls, not a mock state machine: retain every request in this initial burst.
    var ids = engine!.Tracks.SubtitleTracks.Select(t => t.Id).ToArray();
    var burst = Enumerable.Range(0, 50).Select(i => engine.Tracks.SelectSubtitleTrackAsync(
        i % 3 == 0 ? null : ids[i % 2], token).AsTask()).ToArray();
    await Task.WhenAll(burst).WaitAsync(TimeSpan.FromSeconds(20), token);
    counts["subtitle-burst"] = 50;
    using var cadence = new PeriodicTimer(TimeSpan.FromMilliseconds(25));
    while (timer.Elapsed < TimeSpan.FromSeconds(seconds))
    {
        ids = engine.Tracks.SubtitleTracks.Select(t => t.Id).ToArray();
        int? selected = cycles % 3 == 0 ? null : ids[cycles % ids.Length];
        await Measure("subtitle", () => engine.Tracks.SelectSubtitleTrackAsync(selected, token));
        await Measure("seek", () => engine.SeekAsync(TimeSpan.FromSeconds(1 + cycles % 20), token));
        if (cycles % 4 == 0)
        {
            await Measure("pause", () => engine.PauseAsync(token));
            await Measure("play", () => engine.PlayAsync(token));
        }
        await Measure("diagnostics", async () => { await engine.Diagnostics.RefreshAsync(token); });
        if (cycles % 100 == 0)
        {
            await WaitUntil(() => engine.Tracks.SelectedSubtitleTrackId == selected,
                () => engine.Tracks.RefreshAsync(token));
            using var cancelledRequest = new CancellationTokenSource();
            cancelledRequest.Cancel();
            try { await engine.Tracks.SelectSubtitleTrackAsync(ids[0], cancelledRequest.Token); throw new Exception("Cancelled request executed"); }
            catch (OperationCanceledException) { cancelled++; }
        }
        if (cycles > 0 && cycles % 400 == 0)
        {
            var old = engine;
            var refresh = old.Tracks.RefreshAsync(token).AsTask();
            var dispose = old.DisposeAsync().AsTask();
            await Task.WhenAll(refresh, dispose).WaitAsync(TimeSpan.FromSeconds(10), token);
            counts["dispose/recreate"] = counts.GetValueOrDefault("dispose/recreate") + 1;
            await Open();
        }
        cycles++;
        if (cycles % 400 == 0) Console.WriteLine($"elapsed={timer.Elapsed.TotalSeconds:F1}s cycles={cycles} failures={failures.Count}");
        await cadence.WaitForNextTickAsync(token);
    }
}
catch (Exception exception)
{
    failures.Add(exception.GetType().Name + ": " + exception.Message);
    Console.Error.WriteLine(exception);
}
finally
{
    if (engine is not null) await engine.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
    samples.Sort();
    var result = new
    {
        passed = failures.Count == 0 && timer.Elapsed.TotalSeconds >= seconds,
        elapsedSeconds = timer.Elapsed.TotalSeconds, cycles, counts, cancelled, events,
        operationP95Ms = samples.Count == 0 ? 0 : samples[(int)((samples.Count - 1) * .95)],
        operationMaxMs = samples.Count == 0 ? 0 : samples[^1], failures,
        scope = "Real LibVLC video/audio/subtitle decoding with dummy outputs. No WinUI/window/fullscreen acceptance.",
        trace = PlaybackTrace.LogPath
    };
    await File.WriteAllTextAsync(report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    await PlaybackTrace.FlushAsync();
    Console.WriteLine(JsonSerializer.Serialize(result));
    Environment.ExitCode = result.passed ? 0 : 1;
}

