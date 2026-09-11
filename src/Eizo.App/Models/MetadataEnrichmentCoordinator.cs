using System.Collections.Concurrent;
using System.Threading.Channels;
using Eizo.MetadataIntegration;
using Eizo.Recognition;

namespace Eizo.Models;

/// <summary>
/// Low-priority catalog enrichment loop. Source discovery and playback never await this
/// coordinator: scans publish Recognition snapshots first, then this worker enriches them
/// one-by-one using cached provider lookups.
/// </summary>
internal sealed class MetadataEnrichmentCoordinator : IDisposable
{
    private readonly MediaCatalogStore _catalog;
    private readonly MediaMetadataService _service;
    private readonly Channel<string> _queue;
    private readonly ConcurrentDictionary<string, byte> _queued =
        new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;
    private DateTimeOffset _networkSuspendedUntilUtc;
    private bool _disposed;

    public MetadataEnrichmentCoordinator(
        MediaCatalogStore catalog,
        MediaMetadataService service)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _service = service ?? throw new ArgumentNullException(nameof(service));

        _queue = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });

        _catalog.MetadataEnrichmentRequested += Catalog_MetadataEnrichmentRequested;
        _worker = Task.Run(WorkerAsync);
        ScheduleCurrentCatalog();
    }

    public int PendingCount => _queued.Count;

    public bool IsAvailable => _service.IsAvailable;

    private void Catalog_MetadataEnrichmentRequested(
        object? sender,
        EventArgs e) =>
        ScheduleCurrentCatalog();

    private void ScheduleCurrentCatalog()
    {
        if (_disposed || !_service.IsAvailable)
        {
            return;
        }

        foreach (var item in _catalog.Snapshot())
        {
            if (!NeedsEnrichment(item))
            {
                continue;
            }

            var key = MediaCatalogStore.ItemKey(item);
            if (_queued.TryAdd(key, 0))
            {
                _queue.Writer.TryWrite(key);
            }
        }
    }

    private bool NeedsEnrichment(CatalogMediaItemModel item)
    {
        if (item.Recognition is not
            {
                Status: MediaRecognitionStatus.Recognized,
                Title.Length: > 0,
                ConfidenceLevel: "Medium" or "High",
            } recognition)
        {
            return false;
        }

        return item.Metadata is null ||
               !string.Equals(
                   item.Metadata.RuntimeVersion,
                   MediaMetadataService.RuntimeVersion,
                   StringComparison.OrdinalIgnoreCase) ||
               !item.Metadata.MatchesRecognitionRuntime(
                   recognition.RuntimeVersion);
    }

    private async Task WorkerAsync()
    {
        try
        {
            await foreach (var key in _queue.Reader.ReadAllAsync(_shutdown.Token))
            {
                try
                {
                    if (_networkSuspendedUntilUtc > DateTimeOffset.UtcNow)
                    {
                        continue;
                    }

                    var item = _catalog.FindByKey(key);
                    if (item?.Recognition is null ||
                        !NeedsEnrichment(item))
                    {
                        continue;
                    }

                    var metadata = await _service
                        .EnrichAsync(item.Recognition, _shutdown.Token)
                        .ConfigureAwait(false);

                    if (metadata is { IsResolved: true })
                    {
                        _catalog.ApplyMetadata(key, metadata);
                        continue;
                    }

                    if (metadata is
                        {
                            IsResolved: false,
                            Errors.Count: > 0,
                        })
                    {
                        // Do not hammer providers when the machine is offline or an API is
                        // unavailable. Remaining queued work is skipped for this pass and a
                        // later scan/restart can retry.
                        _networkSuspendedUntilUtc =
                            DateTimeOffset.UtcNow.AddMinutes(5);
                    }
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    // Metadata enrichment is disposable background work. Recognition,
                    // library browsing and playback remain authoritative and usable.
                }
                finally
                {
                    _queued.TryRemove(key, out _);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog.MetadataEnrichmentRequested -= Catalog_MetadataEnrichmentRequested;
        _queue.Writer.TryComplete();
        _shutdown.Cancel();

        try
        {
            _worker.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException exception)
            when (exception.InnerExceptions.All(static inner =>
                inner is OperationCanceledException))
        {
        }

        _shutdown.Dispose();
    }
}
