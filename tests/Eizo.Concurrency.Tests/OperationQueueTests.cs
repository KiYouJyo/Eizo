// Cancellation is explicitly controlled by each race test; outer waits have bounded deadlines.
#pragma warning disable xUnit1051
using Eizo.Playback.Core;

namespace Eizo.Concurrency.Tests;

public sealed class OperationQueueTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task NativeCallbackReturnsWithoutWaitingForOwnerAndNeverRunsInline()
    {
        var queue = new PlaybackOperationQueue();
        var entered = Signal();
        var release = Signal();
        var callback = Signal();
        var command = queue.RunAsync("command", async () =>
        {
            Assert.Null(SynchronizationContext.Current);
            entered.SetResult();
            await release.Task;
        }).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        // Simulate LibVLC calling us while the command holds native ownership.
        await Task.Run(() => queue.Post("native-event", () => callback.SetResult()), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(callback.Task.IsCompleted);
        release.SetResult();
        await command;
        await callback.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await queue.CompleteAsync(() => ValueTask.CompletedTask);
    }

    [Fact]
    public async Task ThousandMixedOperationsAreSerialAndFailureDoesNotPoisonQueue()
    {
        var queue = new PlaybackOperationQueue();
        var active = 0;
        var completed = 0;
        var requests = Enumerable.Range(0, 1000).Select(i => queue.RunAsync($"operation-{i}", async () =>
        {
            Assert.Equal(1, Interlocked.Increment(ref active));
            await Task.Yield();
            Interlocked.Increment(ref completed);
            Assert.Equal(0, Interlocked.Decrement(ref active));
        }).AsTask()).ToArray();
        await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.RunAsync("failure",
            () => throw new InvalidOperationException()).AsTask());
        await queue.RunAsync("still-usable", () => ValueTask.CompletedTask);
        Assert.Equal(1000, completed);
        await queue.CompleteAsync(() => ValueTask.CompletedTask);
    }

    [Fact]
    public async Task CancelledWaitersNeverTouchNativeAndDisposeDrainsExactlyOnce()
    {
        var queue = new PlaybackOperationQueue();
        var entered = Signal();
        var release = Signal();
        var current = queue.RunAsync("blocked", async () => { entered.SetResult(); await release.Task; }).AsTask();
        await entered.Task;
        using var cancellation = new CancellationTokenSource();
        var touched = false;
        var waiting = queue.RunAsync("cancelled", () => { touched = true; return ValueTask.CompletedTask; }, cancellation.Token).AsTask();
        cancellation.Cancel();
        var disposed = 0;
        var dispose = queue.CompleteAsync(() => { disposed++; return ValueTask.CompletedTask; });
        Assert.Same(dispose, queue.CompleteAsync(() => throw new InvalidOperationException()));
        Assert.False(dispose.IsCompleted);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queue.RunAsync("late", () => ValueTask.CompletedTask).AsTask());
        release.SetResult();
        await current;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        await dispose.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(touched);
        Assert.Equal(1, disposed);
    }

    [Fact]
    public async Task FiftySubtitleRequestsDuringOpenApplyOnlyLatestIntent()
    {
        var session = new PlaybackOperationSession();
        var release = Signal();
        var opening = session.RunAsync("open", _ => release.Task);
        var selected = new List<int>();
        var requests = Enumerable.Range(0, 50).Select(i => session.RunAsync("subtitle", _ =>
        {
            selected.Add(i);
            return Task.CompletedTask;
        }, latest: true)).ToArray();
        release.SetResult();
        await opening;
        for (var i = 0; i < 49; i++) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => requests[i]);
        await requests[49];
        Assert.Equal([49], selected);
    }

    [Fact]
    public async Task DetachCancelsOpenContinuationAndQueuedSeekWithoutTouchingReplacement()
    {
        var session = new PlaybackOperationSession();
        var release = Signal();
        var continued = false;
        var opening = session.RunAsync("open", async token =>
        {
            await release.Task;
            token.ThrowIfCancellationRequested();
            continued = true;
        });
        var seek = session.RunAsync("seek", _ => { continued = true; return Task.CompletedTask; });
        session.Cancel();
        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => seek);
        Assert.False(continued);
        var replacement = new PlaybackOperationSession();
        await replacement.RunAsync("play", _ => Task.CompletedTask);
    }
}
