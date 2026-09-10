$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$patchPath = Join-Path $repoRoot 'eng/playback-patches/0001-playback-concurrency.patch'
$targetPath = 'src/Eizo.Playback.Core/PlaybackOperationQueue.cs'

if (-not (Test-Path -LiteralPath $patchPath -PathType Leaf)) {
    throw "Playback patch was not found: $patchPath"
}

$existing = Get-Content -LiteralPath $patchPath -Raw
if ($existing.Contains("diff --git a/$targetPath b/$targetPath")) {
    Write-Host 'PlaybackOperationQueue patch is already present.'
    exit 0
}

$source = @'
using System.Threading.Channels;

namespace Eizo.Playback.Core;

/// <summary>
/// Serializes access to native playback state. Posted callbacks are always processed
/// asynchronously by the single consumer and therefore never run inline on a native
/// callback thread.
/// </summary>
public sealed class PlaybackOperationQueue
{
    private interface IWorkItem
    {
        ValueTask ExecuteAsync();
    }

    private sealed class WorkItem : IWorkItem
    {
        private readonly Func<ValueTask> _operation;
        private readonly CancellationToken _cancellationToken;
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WorkItem(Func<ValueTask> operation, CancellationToken cancellationToken)
        {
            _operation = operation;
            _cancellationToken = cancellationToken;
        }

        public Task Completion => _completion.Task;

        public async ValueTask ExecuteAsync()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(_cancellationToken);
                return;
            }

            try
            {
                await _operation().ConfigureAwait(false);
                _cancellationToken.ThrowIfCancellationRequested();
                _completion.TrySetResult();
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(_cancellationToken);
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
        }
    }

    private sealed class WorkItem<T> : IWorkItem
    {
        private readonly Func<ValueTask<T>> _operation;
        private readonly CancellationToken _cancellationToken;
        private readonly TaskCompletionSource<T> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WorkItem(Func<ValueTask<T>> operation, CancellationToken cancellationToken)
        {
            _operation = operation;
            _cancellationToken = cancellationToken;
        }

        public Task<T> Completion => _completion.Task;

        public async ValueTask ExecuteAsync()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(_cancellationToken);
                return;
            }

            try
            {
                var result = await _operation().ConfigureAwait(false);
                _cancellationToken.ThrowIfCancellationRequested();
                _completion.TrySetResult(result);
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(_cancellationToken);
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
        }
    }

    private sealed class PostedWorkItem : IWorkItem
    {
        private readonly Action _operation;

        public PostedWorkItem(Action operation)
        {
            _operation = operation;
        }

        public ValueTask ExecuteAsync()
        {
            try
            {
                _operation();
            }
            catch
            {
                // Fire-and-forget native notifications must never poison the queue.
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class CompletionWorkItem : IWorkItem
    {
        private readonly Func<ValueTask> _finalizer;
        private readonly TaskCompletionSource _completion;

        public CompletionWorkItem(Func<ValueTask> finalizer, TaskCompletionSource completion)
        {
            _finalizer = finalizer;
            _completion = completion;
        }

        public async ValueTask ExecuteAsync()
        {
            try
            {
                await _finalizer().ConfigureAwait(false);
                _completion.TrySetResult();
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
        }
    }

    private readonly object _gate = new();
    private readonly Channel<IWorkItem> _channel =
        Channel.CreateUnbounded<IWorkItem>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    private Task? _completion;
    private bool _accepting = true;

    public PlaybackOperationQueue()
    {
        _ = Task.Run(ProcessAsync);
    }

    public ValueTask RunAsync(
        string name,
        Func<ValueTask> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var item = new WorkItem(operation, cancellationToken);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(!_accepting, this);
            if (!_channel.Writer.TryWrite(item))
            {
                throw new ObjectDisposedException(nameof(PlaybackOperationQueue));
            }
        }

        return new ValueTask(item.Completion);
    }

    public ValueTask<T> RunAsync<T>(
        string name,
        Func<ValueTask<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var item = new WorkItem<T>(operation, cancellationToken);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(!_accepting, this);
            if (!_channel.Writer.TryWrite(item))
            {
                throw new ObjectDisposedException(nameof(PlaybackOperationQueue));
            }
        }

        return new ValueTask<T>(item.Completion);
    }

    public void Post(string name, Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        lock (_gate)
        {
            if (!_accepting)
            {
                return;
            }

            _channel.Writer.TryWrite(new PostedWorkItem(operation));
        }
    }

    public Task CompleteAsync(Func<ValueTask> finalizer)
    {
        ArgumentNullException.ThrowIfNull(finalizer);

        lock (_gate)
        {
            if (_completion is not null)
            {
                return _completion;
            }

            _accepting = false;
            var completionSource =
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completionSource.Task;

            if (!_channel.Writer.TryWrite(new CompletionWorkItem(finalizer, completionSource)))
            {
                completionSource.TrySetException(
                    new ObjectDisposedException(nameof(PlaybackOperationQueue)));
            }

            _channel.Writer.TryComplete();
            return _completion;
        }
    }

    private async Task ProcessAsync()
    {
        await foreach (var item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            await item.ExecuteAsync().ConfigureAwait(false);
        }
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
    throw 'PlaybackOperationQueue patch append verification failed.'
}

Write-Host "Appended PlaybackOperationQueue.cs to $patchPath ($($lines.Count) lines)."
