namespace Eizo;

/// <summary>
/// UI-owned session. Native work is asynchronous; every await retains session ownership.
/// A session owns one playback lifetime: cancelling it invalidates every queued and
/// in-flight operation that was started through it, and the <c>latest</c> policy keeps
/// only the newest intent for a named operation family (for example "subtitle").
/// </summary>
internal sealed class PlaybackOperationSession : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<string, CancellationTokenSource> _latest = new();
    private int _disposeState;

    internal CancellationToken Token => _lifetime.Token;

    internal void Cancel()
    {
        if (Volatile.Read(ref _disposeState) == 0)
        {
            _lifetime.Cancel();
        }
    }

    internal async Task RunAsync(string name, Func<CancellationToken, Task> operation, bool latest = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

        using var request = CancellationTokenSource.CreateLinkedTokenSource(Token, cancellationToken);
        if (latest)
        {
            if (_latest.TryGetValue(name, out var previous))
            {
                previous.Cancel();
            }

            _latest[name] = request;
        }

        try
        {
            await _gate.WaitAsync(request.Token);
            try
            {
                request.Token.ThrowIfCancellationRequested();
                await operation(request.Token);
                request.Token.ThrowIfCancellationRequested();
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            if (latest && _latest.TryGetValue(name, out var current) && ReferenceEquals(current, request))
            {
                _latest.Remove(name);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        Cancel();
        foreach (var request in _latest.Values)
        {
            request.Cancel();
        }

        _latest.Clear();
        _lifetime.Dispose();
    }
}
