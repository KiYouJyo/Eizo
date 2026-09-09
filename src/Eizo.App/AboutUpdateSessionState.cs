namespace Eizo;

internal sealed class AboutUpdateSessionState
{
    private static readonly Lazy<AboutUpdateSessionState> LazyDefault = new(() => new AboutUpdateSessionState());
    private readonly ProductAppUpdateService _productUpdateService = new();
    private AppUpdateInfo _productInfo = new(AppUpdateState.NotChecked);
    private AppUpdateInfo _playbackInfo = new(AppUpdateState.NotChecked);
    private double? _productProgress;
    private int _productBusy;
    private int _playbackBusy;

    private AboutUpdateSessionState()
    {
    }

    public static AboutUpdateSessionState Default => LazyDefault.Value;
    public event EventHandler? Changed;

    public AppUpdateInfo ProductInfo
    {
        get => _productInfo;
        private set
        {
            if (_productInfo == value) return;
            _productInfo = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public AppUpdateInfo PlaybackInfo
    {
        get => _playbackInfo;
        private set
        {
            if (_playbackInfo == value) return;
            _playbackInfo = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public double? ProductProgress
    {
        get => _productProgress;
        private set
        {
            if (_productProgress == value) return;
            _productProgress = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool CanOperateProductUpdate => Volatile.Read(ref _productBusy) == 0;
    public bool CanOperatePlaybackUpdate => Volatile.Read(ref _playbackBusy) == 0;

    public async Task CheckPlaybackUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _playbackBusy, 1) != 0) return;

        try
        {
            PlaybackInfo = PlaybackInfo with
            {
                State = AppUpdateState.Checking,
                Detail = null,
                ErrorCode = null
            };

            var release = await GitHubUpdateService.GetLatestReleaseAsync(
                "KiYouJyo/Eizo.Playback",
                cancellationToken: cancellationToken);

            if (release is null)
            {
                PlaybackInfo = new(
                    AppUpdateState.Failed,
                    ErrorCode: "ReleaseNotFound");
                return;
            }

            if (!GitHubUpdateService.TryParseVersionTag(release.TagName, out var remoteVersion))
            {
                PlaybackInfo = new(
                    AppUpdateState.Failed,
                    release.DisplayVersion,
                    ErrorCode: "InvalidReleaseResponse",
                    Release: release);
                return;
            }

            var currentVersion = PlaybackVersionProvider.GetCurrentVersion();
            PlaybackInfo = new(
                remoteVersion > currentVersion
                    ? AppUpdateState.UpdateAvailable
                    : AppUpdateState.UpToDate,
                release.DisplayVersion,
                Release: release);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            PlaybackInfo = new(
                AppUpdateState.Failed,
                ErrorCode: "UnableToContactGitHub",
                Detail: "Timeout");
        }
        catch (HttpRequestException exception)
        {
            PlaybackInfo = new(
                AppUpdateState.Failed,
                ErrorCode: "UnableToContactGitHub",
                Detail: exception.Message);
        }
        catch (OperationCanceledException)
        {
            PlaybackInfo = new(
                AppUpdateState.Cancelled,
                ErrorCode: "Cancelled",
                Detail: "Cancelled");
        }
        finally
        {
            Interlocked.Exchange(ref _playbackBusy, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task CheckProductUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _productBusy, 1) != 0) return;
        try
        {
            ProductProgress = null;
            ProductInfo = ProductInfo with { State = AppUpdateState.Checking, Detail = null, ErrorCode = null };
            ProductInfo = await _productUpdateService.CheckForUpdatesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ProductProgress = null;
            ProductInfo = ProductInfo with
            {
                State = AppUpdateState.Cancelled,
                Detail = "Cancelled",
                ErrorCode = "Cancelled"
            };
        }
        finally
        {
            Interlocked.Exchange(ref _productBusy, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task DownloadProductUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!ProductInfo.IsUpdateAvailable || Interlocked.Exchange(ref _productBusy, 1) != 0) return;
        try
        {
            var progress = new Progress<AppUpdateProgress>(ApplyProductProgress);
            var result = await _productUpdateService.DownloadAndPrepareAsync(progress, cancellationToken);
            ProductProgress = null;
            ProductInfo = ProductInfo with
            {
                State = result.State,
                Detail = result.Detail,
                ErrorCode = result.ErrorCode
            };
        }
        catch (OperationCanceledException)
        {
            ProductProgress = null;
            ProductInfo = ProductInfo with
            {
                State = AppUpdateState.Cancelled,
                Detail = "Cancelled",
                ErrorCode = "Cancelled"
            };
        }
        finally
        {
            Interlocked.Exchange(ref _productBusy, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task InstallProductUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!ProductInfo.IsReadyToInstall || Interlocked.Exchange(ref _productBusy, 1) != 0) return;
        try
        {
            var progress = new Progress<AppUpdateProgress>(ApplyProductProgress);
            var result = await _productUpdateService.InstallPendingAsync(progress, cancellationToken);
            ProductProgress = null;
            ProductInfo = ProductInfo with
            {
                State = result.State,
                Detail = result.Detail,
                ErrorCode = result.ErrorCode
            };
        }
        catch (OperationCanceledException)
        {
            ProductProgress = null;
            ProductInfo = ProductInfo with
            {
                State = AppUpdateState.Cancelled,
                Detail = "Cancelled",
                ErrorCode = "Cancelled"
            };
        }
        finally
        {
            Interlocked.Exchange(ref _productBusy, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyProductProgress(AppUpdateProgress value)
    {
        ProductProgress = value.State == AppUpdateState.Downloading
            ? AppUpdateProgress.NormalizeValue(value.Value)
            : null;
        ProductInfo = ProductInfo with
        {
            State = value.State,
            Detail = value.Detail,
            ErrorCode = null
        };
    }
}
