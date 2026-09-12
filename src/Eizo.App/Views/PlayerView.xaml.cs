using Eizo.Playback.Core;
using Eizo.Localization;
using Eizo.Models;
using Eizo.Playback;
using Eizo.Playback.WinUI;
using Eizo.PlaybackSupport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;

namespace Eizo.Views;

public sealed partial class PlayerView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly SemaphoreSlim _surfaceLifecycleGate = new(1, 1);
    private PlaybackOperationSession _session = new();
    private Task? _detachTask;
    private readonly DispatcherTimer _fullscreenControlsTimer;
    private readonly DispatcherTimer _loadingMetricsTimer;

    private IPlaybackEngine? _engine;
    private PlaybackSource? _currentSource;
    private TimeSpan _lastKnownPosition;
    private TimeSpan _duration;
    private bool _playIntent;
    private bool _isUpdatingTimeline;
    private bool _isUpdatingTrackSelections;
    private bool _trackUiUpdateQueued;
    private string _subtitleTrackListKey = string.Empty;
    private string _audioTrackListKey = string.Empty;
    private ComboBoxItem? _subtitleOffItem;
    private bool _isVideoFullscreen;
    private bool _sidebarCollapsedByUser;
    private bool _sidebarVisibleInFullscreen;
    private bool _hasPointerPosition;
    private Point _lastPointerPosition;
    private double _volume = 1d;
    private bool _isUpdatingVolume;
    private bool _isUpdatingSubtitlePositions;
    private double _primarySubtitleVerticalPosition = 12d;
    private double _secondarySubtitleVerticalPosition = 24d;
    private bool _pointerWheelHooked;
    private bool _isPreparingForDetach;
    private int _fullscreenGeneration;
    private bool _positionUiUpdatePending;
    private PointerEventHandler? _pointerWheelHandler;
    private CancellationTokenSource? _seekDebounce;
    private bool _loadingMetricsRefreshInFlight;
    private bool _isLoadingStatusVisible;
    private long? _lastLoadingReadBytes;
    private DateTimeOffset? _lastLoadingSampleAt;
    private readonly List<PlaybackQueueItemModel> _queueItems;
    private int _queueIndex = -1;
    private bool _isUpdatingQueueSelection;
    private IReadOnlyList<ExternalSubtitleCandidate> _externalSubtitles =
        Array.Empty<ExternalSubtitleCandidate>();
    private SubtitleDocument? _primarySubtitleDocument;
    private Uri? _primarySubtitleUri;
    private int _primarySubtitleGeneration;
    private SubtitleDocument? _secondarySubtitleDocument;
    private Uri? _secondarySubtitleUri;
    private bool _isUpdatingSecondarySubtitleSelection;
    private int _secondarySubtitleGeneration;

    public PlayerView(
        string title,
        string episode,
        PlaybackSource? initialSource = null,
        IReadOnlyList<PlaybackQueueItemModel>? queue = null,
        int initialQueueIndex = 0)
    {
        InitializeComponent();
        InitializeSubtitlePositionControls();

        _queueItems = queue?
            .OrderBy(static item => item.Index)
            .ToList() ?? [];

        if (_queueItems.Count == 0 && initialSource is not null)
        {
            _queueItems.Add(
                new PlaybackQueueItemModel(
                    0,
                    "1",
                    title,
                    string.Empty,
                    episode,
                    initialSource));
        }

        if (_queueItems.Count > 0)
        {
            _queueIndex = Math.Clamp(
                initialQueueIndex,
                0,
                _queueItems.Count - 1);
            _currentSource = _queueItems[_queueIndex].Source;
        }
        else
        {
            _currentSource = initialSource;
        }

        PlaybackSelectionTrace.Write(
            "view-init",
            CurrentQueueItem?.CatalogItem?.SourceTitle ?? episode,
            _currentSource?.Uri.ToString(),
            _queueIndex,
            _queueItems.Count);

        NowPlayingTitle.Text = title;
        NowPlayingEpisode.Text =
            CurrentQueueItem?.Title ??
            episode;

        QueueList.ItemsSource = _queueItems;

        PlaybackSurface.EngineChanged += PlaybackSurface_EngineChanged;
        PlaybackSurface.InitializationFailed += PlaybackSurface_InitializationFailed;

        _fullscreenControlsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _fullscreenControlsTimer.Tick += FullscreenControlsTimer_Tick;

        _loadingMetricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _loadingMetricsTimer.Tick += LoadingMetricsTimer_Tick;

        SubtitleTrackCombo.DropDownClosed += (_, _) =>
        {
            if (_trackUiUpdateQueued)
            {
                _trackUiUpdateQueued = false;
                QueueTrackUiUpdate();
            }
        };
        AudioTrackCombo.DropDownClosed += (_, _) =>
        {
            if (_trackUiUpdateQueued)
            {
                _trackUiUpdateQueued = false;
                QueueTrackUiUpdate();
            }
        };

        Loaded += PlayerView_Loaded;
        Unloaded += PlayerView_Unloaded;

        ApplyText();
        ResetTimeline();
        RebuildSecondarySubtitleCombo();
        RefreshQueueStatus();

        if (_currentSource is not null)
        {
            _playIntent = true;
            ShowLoadingStatus();
        }
        else
        {
            ShowStatus(T("Playback_NoMediaSource"));
        }

        UpdateControlAvailability();
    }

    private PlaybackQueueItemModel? CurrentQueueItem =>
        _queueIndex >= 0 &&
        _queueIndex < _queueItems.Count
            ? _queueItems[_queueIndex]
            : null;

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        QueueTitle.Text = T("Playback_Queue");
        QueueSubtitle.Text = NowPlayingEpisode.Text;
        SubtitleTrackLabel.Text = T("Playback_SubtitleTrack");
        PrimarySubtitlePositionLabel.Text =
            T("Playback_PrimarySubtitlePosition");
        SecondarySubtitleTrackLabel.Text = T("Playback_SecondarySubtitle");
        SecondarySubtitlePositionLabel.Text =
            T("Playback_SecondarySubtitlePosition");
        AudioTrackLabel.Text = T("Playback_AudioTrack");
        PlaybackRateFlyoutTitle.Text = T("Playback_Rate");
        VolumeFlyoutTitle.Text = T("Playback_Volume");

        PlayerSectionList.ItemsSource = new[]
        {
            T("Playback_Queue"),
            T("Playback_Tracks")
        };

        UpdateSidebarSectionUi();

        ToolTipService.SetToolTip(PreviousChapterButton, T("Playback_PreviousChapter"));
        ToolTipService.SetToolTip(PreviousJumpButton, T("Playback_Back10Seconds"));
        ToolTipService.SetToolTip(PlayPauseButton, T("Common_Play"));
        ToolTipService.SetToolTip(NextJumpButton, T("Playback_Forward10Seconds"));
        ToolTipService.SetToolTip(NextChapterButton, T("Playback_NextChapter"));
        ToolTipService.SetToolTip(VolumeButton, T("Playback_Volume"));
        ToolTipService.SetToolTip(SidebarToggleButton, T("Playback_CollapseSidebar"));
        ToolTipService.SetToolTip(FullscreenButton, T("Playback_FullScreen"));

        AutomationProperties.SetName(PreviousChapterButton, T("Playback_PreviousChapter"));
        AutomationProperties.SetName(PreviousJumpButton, T("Playback_Back10Seconds"));
        AutomationProperties.SetName(PlayPauseButton, T("Common_Play"));
        AutomationProperties.SetName(NextJumpButton, T("Playback_Forward10Seconds"));
        AutomationProperties.SetName(NextChapterButton, T("Playback_NextChapter"));
        AutomationProperties.SetName(VolumeButton, T("Playback_Volume"));
        AutomationProperties.SetName(SidebarToggleButton, T("Playback_CollapseSidebar"));
        AutomationProperties.SetName(FullscreenButton, T("Playback_FullScreen"));
    }

    private async void PlaybackSurface_EngineChanged(
        object? sender,
        PlaybackViewEngineChangedEventArgs e)
    {
        // EngineChanged and PrepareForDetach both mutate _engine/_session. Serialize
        // only the mutation section; opening a source must not block surface teardown.
        await _surfaceLifecycleGate.WaitAsync();
        try
        {
            _session.Cancel();
            _seekDebounce?.Cancel();
            if (e.PreviousEngine is not null)
                DetachEngine(e.PreviousEngine);
            if (_isPreparingForDetach) return;
            _session = new PlaybackOperationSession();

            _engine = e.CurrentEngine;

            if (e.CurrentEngine is null)
            {
                UpdateControlAvailability();
                return;
            }

            AttachEngine(e.CurrentEngine);
            UpdateControlAvailability();
        }
        finally
        {
            _surfaceLifecycleGate.Release();
        }

        if (e.CurrentEngine is null)
        {
            return;
        }

        if (_currentSource is null)
        {
            ShowStatus(T("Playback_NoMediaSource"));
            return;
        }

        await RestoreSourceOnEngineAsync(e.CurrentEngine);
    }

    private void PlaybackSurface_InitializationFailed(
        object? sender,
        PlaybackViewInitializationFailedEventArgs e)
    {
        ShowStatus(T("Status_Error"));
    }

    private void AttachEngine(IPlaybackEngine engine)
    {
        engine.StateChanged += Engine_StateChanged;
        engine.PositionChanged += Engine_PositionChanged;
        engine.DurationChanged += Engine_DurationChanged;
        engine.Failed += Engine_Failed;

        engine.Tracks.TracksChanged += Tracks_TracksChanged;
        engine.Navigation.NavigationChanged += Navigation_NavigationChanged;
        engine.Diagnostics.DiagnosticsChanged += Diagnostics_DiagnosticsChanged;

        try
        {
            engine.Volume = _volume;
        }
        catch (Exception exception)
        { PlaybackTrace.Write("view", "control", "error", exception.GetType().Name); }

        Dispatch(() =>
        {
            UpdateStateUi(engine.State);
            QueueTrackUiUpdate();
            UpdateNavigationAvailability();
            UpdateDiagnosticsUi(engine.Diagnostics.Current);
            PlaybackRateSlider.Value = RateToSliderValue(engine.PlaybackRate);
            UpdateVolumeUi(_volume);
        });
    }

    private void DetachEngine(IPlaybackEngine engine)
    {
        engine.StateChanged -= Engine_StateChanged;
        engine.PositionChanged -= Engine_PositionChanged;
        engine.DurationChanged -= Engine_DurationChanged;
        engine.Failed -= Engine_Failed;

        try
        {
            engine.Tracks.TracksChanged -= Tracks_TracksChanged;
            engine.Navigation.NavigationChanged -= Navigation_NavigationChanged;
            engine.Diagnostics.DiagnosticsChanged -= Diagnostics_DiagnosticsChanged;
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void Engine_StateChanged(object? sender, PlaybackStateChangedEventArgs e) =>
        DispatchEngine(sender, () =>
        {
            if (e.CurrentState == PlaybackState.Ended)
            {
                if (_queueIndex >= 0 &&
                    _queueIndex < _queueItems.Count - 1)
                {
                    _ = SwitchQueueItemAsync(
                        _queueIndex + 1,
                        autoplay: true);
                    return;
                }

                _playIntent = false;
            }

            UpdateStateUi(e.CurrentState);
        });

    private void Engine_PositionChanged(object? sender, PlaybackPositionChangedEventArgs e)
    {
        // LibVLC raises TimeChanged far more often than the UI can paint. Coalesce to
        // one queued update so rapid position events never pile up on the dispatcher.
        if (_positionUiUpdatePending || _isPreparingForDetach || _engine is not { } engine)
            return;
        if (!ReferenceEquals(sender, engine)) return;

        _positionUiUpdatePending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _positionUiUpdatePending = false;
            if (_isPreparingForDetach || !ReferenceEquals(_engine, engine)) return;
            if (_seekDebounce is not null) return;
            _lastKnownPosition = e.Position;
            CurrentTimeText.Text = FormatTime(e.Position);
            UpdatePrimarySubtitle(e.Position);
            UpdateSecondarySubtitle(e.Position);
            if (_duration <= TimeSpan.Zero) return;
            _isUpdatingTimeline = true;
            try { PlaybackSlider.Value = Math.Clamp(e.Position.TotalSeconds, PlaybackSlider.Minimum, PlaybackSlider.Maximum); }
            finally { _isUpdatingTimeline = false; }
        });
    }

    private void Engine_DurationChanged(object? sender, PlaybackDurationChangedEventArgs e) =>
        DispatchEngine(sender, () =>
        {
            _duration = e.Duration;
            _isUpdatingTimeline = true;
            try
            {
                PlaybackSlider.Maximum = Math.Max(1d, e.Duration.TotalSeconds);
                DurationText.Text = FormatTime(e.Duration);
            }
            finally { _isUpdatingTimeline = false; }
        });

    private void Engine_Failed(object? sender, PlaybackFailedEventArgs e) =>
        DispatchEngine(sender, () => { _playIntent = false; ShowStatus(T("Status_Error")); });

    private void Tracks_TracksChanged(object? sender, PlaybackTracksChangedEventArgs e) =>
        DispatchEngine(sender, QueueTrackUiUpdate);

    private void Navigation_NavigationChanged(object? sender, PlaybackNavigationChangedEventArgs e) =>
        DispatchEngine(sender, UpdateNavigationAvailability);

    private void Diagnostics_DiagnosticsChanged(object? sender, PlaybackDiagnosticsChangedEventArgs e) =>
        DispatchEngine(sender, () => UpdateDiagnosticsUi(e.Snapshot));

    private void DispatchEngine(object? sender, Action action)
    {
        // Check identity on the UI thread, after dequeue; old callbacks cannot mutate a new session.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isPreparingForDetach || _engine is not { } engine) return;
            if (ReferenceEquals(sender, engine) || ReferenceEquals(sender, engine.Tracks) ||
                ReferenceEquals(sender, engine.Navigation) || ReferenceEquals(sender, engine.Diagnostics)) action();
        });
    }

    private async Task RunOperationAsync(IPlaybackEngine engine, string name,
        Func<CancellationToken, Task> action, bool latest = false, CancellationToken token = default)
    {
        var session = _session;
        if (_isPreparingForDetach || !ReferenceEquals(_engine, engine)) return;
        PlaybackTrace.Write("view", name, "requested");
        try
        {
            await session.RunAsync(name, action, latest, token);
            PlaybackTrace.Write("view", name, "complete");
        }
        catch (OperationCanceledException) when (session.Token.IsCancellationRequested || latest || token.IsCancellationRequested)
        { PlaybackTrace.Write("view", name, "cancel"); }
        catch (Exception exception)
        {
            PlaybackTrace.Write("view", name, "error", exception.GetType().Name);
            if (ReferenceEquals(_session, session) && !_isPreparingForDetach) ShowStatus(T("Status_Error"));
        }
    }

    private async Task RestoreSourceOnEngineAsync(IPlaybackEngine engine)
    {
        try
        {
            ShowLoadingStatus();
            await OpenSourceOnEngineAsync(engine, restorePosition: true);
        }
        catch
        {
            _playIntent = false;
            ShowStatus(T("Status_Error"));
        }
    }

    private Task OpenSourceOnEngineAsync(
        IPlaybackEngine engine,
        bool restorePosition,
        bool latest = false)
    {
        var source = _currentSource;
        var queueItem = CurrentQueueItem;
        var catalogItem = queueItem?.CatalogItem;
        var resumePosition = restorePosition
            ? _lastKnownPosition
            : TimeSpan.Zero;

        if (source is null)
            return Task.CompletedTask;

        return RunOperationAsync(
            engine,
            "open",
            async token =>
            {
                await engine.OpenAsync(source, token);
                token.ThrowIfCancellationRequested();

                await engine.PlayAsync(token);
                if (resumePosition > TimeSpan.FromMilliseconds(250))
                    await TryRestorePositionAsync(
                        engine,
                        resumePosition,
                        token);

                token.ThrowIfCancellationRequested();
                if (!_playIntent)
                    await engine.PauseAsync(token);

                await DiscoverAndAttachExternalSubtitlesAsync(
                    engine,
                    source,
                    catalogItem,
                    token);

                await engine.Tracks.RefreshAsync(token);
                await engine.Navigation.RefreshAsync(token);
                await engine.Diagnostics.RefreshAsync(token);
            },
            latest: latest);
    }

    private async Task DiscoverAndAttachExternalSubtitlesAsync(
        IPlaybackEngine engine,
        PlaybackSource source,
        CatalogMediaItemModel? catalogItem,
        CancellationToken token)
    {
        IReadOnlyList<ExternalSubtitleCandidate> candidates;

        try
        {
            candidates = await ExternalSubtitleService.DiscoverAsync(
                source,
                catalogItem,
                token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            PlaybackTrace.Write(
                "view",
                "external-subtitles",
                "discover-error",
                exception.GetType().Name);
            candidates = Array.Empty<ExternalSubtitleCandidate>();
        }

        token.ThrowIfCancellationRequested();

        SubtitleDocument? automaticPrimaryDocument = null;
        ExternalSubtitleCandidate? automaticPrimaryCandidate = null;

        // External subtitles are rendered by Eizo's WinUI overlay rather than
        // registered with LibVLC. This keeps ASS/SRT/VTT/SSA typography
        // consistent with the second-subtitle renderer.
        if (engine.Tracks.SelectedSubtitleTrackId is null &&
            candidates.Count > 0)
        {
            automaticPrimaryCandidate = candidates[0];

            try
            {
                automaticPrimaryDocument =
                    await ExternalSubtitleService.LoadDocumentAsync(
                        automaticPrimaryCandidate,
                        token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                PlaybackTrace.Write(
                    "view",
                    "primary-subtitle",
                    "auto-load-error",
                    exception.GetType().Name);
            }
        }

        token.ThrowIfCancellationRequested();

        Dispatch(() =>
        {
            if (_currentSource is null ||
                _currentSource.Uri != source.Uri)
            {
                return;
            }

            _externalSubtitles = candidates;

            if (_primarySubtitleUri is null &&
                automaticPrimaryCandidate is not null &&
                automaticPrimaryDocument is not null)
            {
                _primarySubtitleUri = automaticPrimaryCandidate.Uri;
                _primarySubtitleDocument = automaticPrimaryDocument;
                UpdatePrimarySubtitle(_lastKnownPosition);
            }

            RebuildSecondarySubtitleCombo();
            _subtitleTrackListKey = string.Empty;
            QueueTrackUiUpdate();
            UpdateControlAvailability();
            PlaybackTrace.Write(
                "view",
                "external-subtitles",
                "complete",
                candidates.Count.ToString());
        });
    }

    private static async Task TryRestorePositionAsync(IPlaybackEngine engine, TimeSpan position, CancellationToken token)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnostics = engine.Diagnostics;
        void OnDiagnostics(object? sender, PlaybackDiagnosticsChangedEventArgs e)
        {
            if (e.Snapshot.Capabilities.CanSeek) ready.TrySetResult();
        }
        diagnostics.DiagnosticsChanged += OnDiagnostics;
        try
        {
            if ((await diagnostics.RefreshAsync(token)).Capabilities.CanSeek) ready.TrySetResult();
            try { await ready.Task.WaitAsync(TimeSpan.FromSeconds(2), token); }
            catch (TimeoutException)
            {
                PlaybackTrace.Write("view", "restore-position", "not-seekable");
                return;
            }
            await engine.SeekAsync(position, token);
        }
        finally { diagnostics.DiagnosticsChanged -= OnDiagnostics; }
    }

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        await TogglePlayPauseAsync();
    }

    internal async Task TogglePlayPauseAsync()
    {
        if (_currentSource is null)
            return;

        if (_engine is not { } engine)
            return;

        _playIntent = !_playIntent;
        var shouldPlay = _playIntent;
        await RunOperationAsync(engine, "play-intent", async token =>
        {
            if (shouldPlay) await engine.PlayAsync(token);
            else await engine.PauseAsync(token);
        }, latest: true);
    }

    internal Button PlayPauseButtonElement => PlayPauseButton;

    internal bool IsVideoFullscreen => _isVideoFullscreen;

    internal void ExitFullscreenFromKeyboard()
    {
        if (!_isVideoFullscreen || _isPreparingForDetach)
            return;

        SetVideoFullscreen(false);
    }

    private async void PreviousJumpButton_Click(object sender, RoutedEventArgs e) =>
        await SeekRelativeAsync(TimeSpan.FromSeconds(-10));

    private async void NextJumpButton_Click(object sender, RoutedEventArgs e) =>
        await SeekRelativeAsync(TimeSpan.FromSeconds(10));

    private async Task SeekRelativeAsync(TimeSpan delta)
    {
        if (_engine is not { } engine)
            return;

        try
        {
            var target = engine.Position + delta;

            if (target < TimeSpan.Zero)
                target = TimeSpan.Zero;

            if (engine.Duration > TimeSpan.Zero && target > engine.Duration)
                target = engine.Duration;

            await RunOperationAsync(engine, "seek", async token => await engine.SeekAsync(target, token), latest: true);
        }
        catch (Exception exception)
        { PlaybackTrace.Write("view", "control", "error", exception.GetType().Name); }
    }

    private async void PreviousChapterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine is not { } engine)
            return;

        if (engine.Navigation.Chapters.Count == 0)
        {
            if (_queueIndex > 0)
            {
                await SwitchQueueItemAsync(
                    _queueIndex - 1,
                    autoplay: true);
            }

            return;
        }

        try
        {
            await RunOperationAsync(
                engine,
                "chapter",
                async token =>
                {
                    await engine.Navigation.PreviousChapterAsync(
                        token);
                });
        }
        catch (Exception exception)
        {
            PlaybackTrace.Write(
                "view",
                "control",
                "error",
                exception.GetType().Name);
        }
    }

    private async void NextChapterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine is not { } engine)
            return;

        if (engine.Navigation.Chapters.Count == 0)
        {
            if (_queueIndex >= 0 &&
                _queueIndex < _queueItems.Count - 1)
            {
                await SwitchQueueItemAsync(
                    _queueIndex + 1,
                    autoplay: true);
            }

            return;
        }

        try
        {
            await RunOperationAsync(
                engine,
                "chapter",
                async token =>
                {
                    await engine.Navigation.NextChapterAsync(
                        token);
                });
        }
        catch (Exception exception)
        {
            PlaybackTrace.Write(
                "view",
                "control",
                "error",
                exception.GetType().Name);
        }
    }

    private async void PlaybackSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingTimeline ||
            _engine is not { } engine ||
            _duration <= TimeSpan.Zero)
        {
            return;
        }

        _seekDebounce?.Cancel();
        var request = new CancellationTokenSource();
        _seekDebounce = request;
        var token = request.Token;
        var target = TimeSpan.FromSeconds(e.NewValue);

        _lastKnownPosition = target;
        CurrentTimeText.Text = FormatTime(target);
        UpdatePrimarySubtitle(target);
        UpdateSecondarySubtitle(target);

        try
        {
            await Task.Delay(80, token);
            await RunOperationAsync(engine, "seek", async ct => await engine.SeekAsync(target, ct), latest: true, token: token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        { PlaybackTrace.Write("view", "seek", "error", exception.GetType().Name); }
        finally
        {
            if (ReferenceEquals(_seekDebounce, request)) _seekDebounce = null;
            request.Dispose();
        }
    }

    private async void SubtitleTrackCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isUpdatingTrackSelections ||
            _engine is not { } engine ||
            SubtitleTrackCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var generation = ++_primarySubtitleGeneration;

        if (item.Tag is ExternalSubtitleCandidate candidate)
        {
            _primarySubtitleUri = candidate.Uri;
            _primarySubtitleDocument = null;

            if (_secondarySubtitleUri == candidate.Uri)
            {
                _secondarySubtitleGeneration++;
                _secondarySubtitleUri = null;
                _secondarySubtitleDocument = null;
                UpdateSecondarySubtitle(_lastKnownPosition);
            }

            RebuildSecondarySubtitleCombo();

            try
            {
                await RunOperationAsync(
                    engine,
                    "subtitle",
                    async token =>
                        await engine.Tracks.SelectSubtitleTrackAsync(
                            null,
                            token),
                    latest: true);

                var session = _session;
                var document =
                    await ExternalSubtitleService.LoadDocumentAsync(
                        candidate,
                        session.Token);

                if (generation != _primarySubtitleGeneration ||
                    !ReferenceEquals(session, _session) ||
                    session.Token.IsCancellationRequested)
                {
                    return;
                }

                _primarySubtitleDocument = document;
                UpdatePrimarySubtitle(_lastKnownPosition);
                QueueTrackUiUpdate();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                PlaybackTrace.Write(
                    "view",
                    "primary-subtitle",
                    "error",
                    exception.GetType().Name);
            }

            return;
        }

        _primarySubtitleUri = null;
        _primarySubtitleDocument = null;
        UpdatePrimarySubtitle(_lastKnownPosition);
        RebuildSecondarySubtitleCombo();

        try
        {
            var selected = item.Tag is int id ? id : (int?)null;
            await RunOperationAsync(
                engine,
                "subtitle",
                async token =>
                    await engine.Tracks.SelectSubtitleTrackAsync(
                        selected,
                        token),
                latest: true);
        }
        catch
        {
            ShowStatus(T("Status_Error"));
        }
    }

    private async void SecondarySubtitleCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isUpdatingSecondarySubtitleSelection)
            return;

        var generation = ++_secondarySubtitleGeneration;

        if (SecondarySubtitleCombo.SelectedItem is not ComboBoxItem
            {
                Tag: ExternalSubtitleCandidate candidate
            })
        {
            _secondarySubtitleDocument = null;
            _secondarySubtitleUri = null;
            UpdateSecondarySubtitle(_lastKnownPosition);
            return;
        }

        var session = _session;

        try
        {
            var document =
                await ExternalSubtitleService.LoadDocumentAsync(
                    candidate,
                    session.Token);

            if (generation != _secondarySubtitleGeneration ||
                !ReferenceEquals(session, _session) ||
                session.Token.IsCancellationRequested)
            {
                return;
            }

            _secondarySubtitleDocument = document;
            _secondarySubtitleUri = candidate.Uri;
            UpdateSecondarySubtitle(_lastKnownPosition);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            PlaybackTrace.Write(
                "view",
                "secondary-subtitle",
                "error",
                exception.GetType().Name);
        }
    }

    private async void AudioTrackCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isUpdatingTrackSelections ||
            _engine is not { } engine ||
            AudioTrackCombo.SelectedItem is not ComboBoxItem { Tag: int id })
        {
            return;
        }

        try
        {
            await RunOperationAsync(engine, "audio", async token => await engine.Tracks.SelectAudioTrackAsync(id, token), latest: true);
        }
        catch
        {
            ShowStatus(T("Status_Error"));
        }
    }

    private void QueueTrackUiUpdate()
    {
        if (_trackUiUpdateQueued || _isPreparingForDetach)
            return;

        _trackUiUpdateQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _trackUiUpdateQueued = false;
            if (_isPreparingForDetach)
                return;

            // Rebuilding an open ComboBox while WinUI is closing its popup is the
            // 0xc000027b crash source after subtitle/audio switching. Defer until the
            // drop-down has fully closed, then apply the freshest track snapshot.
            if (SubtitleTrackCombo.IsDropDownOpen || AudioTrackCombo.IsDropDownOpen)
            {
                _trackUiUpdateQueued = true;
                return;
            }

            UpdateTrackUi();
        });
    }

    private void UpdateTrackUi()
    {
        if (_engine is not { } engine)
            return;

        var tracks = engine.Tracks;
        var subtitleKey =
            BuildTrackListKey(
                tracks.SubtitleTracks.Select(static track => track.Id)) +
            "|ext:" +
            string.Join(
                "|",
                _externalSubtitles.Select(
                    static candidate => candidate.Uri.AbsoluteUri));
        var audioKey = BuildTrackListKey(tracks.AudioTracks.Select(static track => track.Id));

        _isUpdatingTrackSelections = true;
        try
        {
            // A selection change does not alter the track list. Rebuilding the whole
            // ComboBox here is what crashes WinUI 3 (0xc000027b) when the drop-down
            // popup is still closing; for selection-only changes we just sync the
            // SelectedItem and never touch Items.
            if (!string.Equals(subtitleKey, _subtitleTrackListKey, StringComparison.Ordinal))
            {
                RebuildSubtitleCombo(tracks);
                _subtitleTrackListKey = subtitleKey;
            }
            else
            {
                SyncSubtitleSelection(tracks);
            }

            if (!string.Equals(audioKey, _audioTrackListKey, StringComparison.Ordinal))
            {
                RebuildAudioCombo(tracks);
                _audioTrackListKey = audioKey;
            }
            else
            {
                SyncAudioSelection(tracks);
            }
        }
        finally
        {
            _isUpdatingTrackSelections = false;
        }
    }

    private static string BuildTrackListKey(IEnumerable<int> ids) =>
        string.Join(",", ids);

    private void RebuildSubtitleCombo(IPlaybackTrackController tracks)
    {
        SubtitleTrackCombo.Items.Clear();

        _subtitleOffItem = new ComboBoxItem
        {
            Content = T("Playback_NoSubtitle"),
            Tag = null
        };

        SubtitleTrackCombo.Items.Add(_subtitleOffItem);

        ComboBoxItem? selected = _subtitleOffItem;

        foreach (var track in tracks.SubtitleTracks)
        {
            var item = new ComboBoxItem
            {
                Content = FormatSubtitleTrack(track),
                Tag = track.Id
            };

            SubtitleTrackCombo.Items.Add(item);

            if (_primarySubtitleUri is null &&
                tracks.SelectedSubtitleTrackId == track.Id)
            {
                selected = item;
            }
        }

        foreach (var candidate in _externalSubtitles)
        {
            var item = new ComboBoxItem
            {
                Content = FormatExternalSubtitleCandidate(candidate),
                Tag = candidate
            };

            SubtitleTrackCombo.Items.Add(item);

            if (_primarySubtitleUri == candidate.Uri)
                selected = item;
        }

        SubtitleTrackCombo.SelectedItem = selected;
    }

    private void RebuildSecondarySubtitleCombo()
    {
        if (SecondarySubtitleCombo is null)
            return;

        _isUpdatingSecondarySubtitleSelection = true;

        try
        {
            SecondarySubtitleCombo.Items.Clear();

            var off = new ComboBoxItem
            {
                Content = T("Playback_NoSubtitle"),
                Tag = null
            };
            SecondarySubtitleCombo.Items.Add(off);

            ComboBoxItem? selected = off;

            foreach (var candidate in _externalSubtitles)
            {
                if (_primarySubtitleUri == candidate.Uri)
                    continue;
                var item = new ComboBoxItem
                {
                    Content = FormatExternalSubtitleCandidate(candidate),
                    Tag = candidate
                };

                SecondarySubtitleCombo.Items.Add(item);

                if (_secondarySubtitleUri is not null &&
                    candidate.Uri == _secondarySubtitleUri)
                {
                    selected = item;
                }
            }

            SecondarySubtitleCombo.SelectedItem = selected;
        }
        finally
        {
            _isUpdatingSecondarySubtitleSelection = false;
        }
    }

    private static string FormatExternalSubtitleCandidate(
        ExternalSubtitleCandidate candidate) =>
        string.IsNullOrWhiteSpace(candidate.Language)
            ? candidate.DisplayName
            : candidate.DisplayName + " · " + candidate.Language;

    private void UpdatePrimarySubtitle(TimeSpan position)
    {
        var text = _primarySubtitleDocument?.GetText(position);

        if (string.IsNullOrWhiteSpace(text))
        {
            PrimarySubtitleText.Text = string.Empty;
            PrimarySubtitleOverlay.Visibility =
                Visibility.Collapsed;
            return;
        }

        PrimarySubtitleText.Text = text;
        PrimarySubtitleOverlay.Visibility =
            Visibility.Visible;
    }

    private void UpdateSecondarySubtitle(TimeSpan position)
    {
        var text = _secondarySubtitleDocument?.GetText(position);

        if (string.IsNullOrWhiteSpace(text))
        {
            SecondarySubtitleText.Text = string.Empty;
            SecondarySubtitleOverlay.Visibility =
                Visibility.Collapsed;
            return;
        }

        SecondarySubtitleText.Text = text;
        SecondarySubtitleOverlay.Visibility =
            Visibility.Visible;
    }

    private void ResetSecondarySubtitleState()
    {
        _primarySubtitleGeneration++;
        _primarySubtitleDocument = null;
        _primarySubtitleUri = null;
        _secondarySubtitleGeneration++;
        _secondarySubtitleDocument = null;
        _secondarySubtitleUri = null;
        _externalSubtitles =
            Array.Empty<ExternalSubtitleCandidate>();

        if (PrimarySubtitleText is not null)
            PrimarySubtitleText.Text = string.Empty;

        if (PrimarySubtitleOverlay is not null)
            PrimarySubtitleOverlay.Visibility =
                Visibility.Collapsed;

        if (SecondarySubtitleText is not null)
            SecondarySubtitleText.Text = string.Empty;

        if (SecondarySubtitleOverlay is not null)
            SecondarySubtitleOverlay.Visibility =
                Visibility.Collapsed;

        _subtitleTrackListKey = string.Empty;
        RebuildSecondarySubtitleCombo();
    }

    private void RebuildAudioCombo(IPlaybackTrackController tracks)
    {
        AudioTrackCombo.Items.Clear();
        ComboBoxItem? selected = null;

        foreach (var track in tracks.AudioTracks)
        {
            var item = new ComboBoxItem
            {
                Content = FormatAudioTrack(track),
                Tag = track.Id
            };

            AudioTrackCombo.Items.Add(item);
            if (tracks.SelectedAudioTrackId == track.Id)
                selected = item;
        }

        if (AudioTrackCombo.Items.Count == 0)
        {
            AudioTrackCombo.Items.Add(
                new ComboBoxItem
                {
                    Content = "—",
                    IsEnabled = false
                });
        }

        AudioTrackCombo.SelectedItem =
            selected ??
            (AudioTrackCombo.Items.Count > 0
                ? AudioTrackCombo.Items[0]
                : null);
    }

    private void SyncSubtitleSelection(IPlaybackTrackController tracks)
    {
        if (SubtitleTrackCombo.IsDropDownOpen)
            return;

        ComboBoxItem? target;

        if (_primarySubtitleUri is not null)
        {
            target = SubtitleTrackCombo.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item =>
                    item.Tag is ExternalSubtitleCandidate candidate &&
                    candidate.Uri == _primarySubtitleUri);
        }
        else
        {
            target = tracks.SelectedSubtitleTrackId is int id
                ? SubtitleTrackCombo.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(item =>
                        item.Tag is int tag &&
                        tag == id)
                : _subtitleOffItem;
        }

        if (target is not null &&
            !ReferenceEquals(
                SubtitleTrackCombo.SelectedItem,
                target))
        {
            SubtitleTrackCombo.SelectedItem = target;
        }
    }

    private void SyncAudioSelection(IPlaybackTrackController tracks)
    {
        if (AudioTrackCombo.IsDropDownOpen)
            return;

        var target = tracks.SelectedAudioTrackId is int selectedId
            ? AudioTrackCombo.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag is int tag && tag == selectedId)
            : null;

        if (target is null && AudioTrackCombo.Items.Count > 0)
            target = AudioTrackCombo.Items[0] as ComboBoxItem;

        if (target is not null && !ReferenceEquals(AudioTrackCombo.SelectedItem, target))
            AudioTrackCombo.SelectedItem = target;
    }

    private MenuFlyout BuildSubtitleFlyout(IPlaybackTrackController tracks)
    {
        var flyout = new MenuFlyout();

        var off = new RadioMenuFlyoutItem
        {
            Text = T("Playback_NoSubtitle"),
            GroupName = "subtitle",
            IsChecked = tracks.SelectedSubtitleTrackId is null
        };

        off.Click += async (_, _) =>
        {
            if (_engine is { } engine)
                await TrySelectSubtitleAsync(engine, null);
        };

        flyout.Items.Add(off);

        foreach (var track in tracks.SubtitleTracks)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = FormatSubtitleTrack(track),
                GroupName = "subtitle",
                IsChecked = track.IsSelected
            };

            var trackId = track.Id;
            item.Click += async (_, _) =>
            {
                if (_engine is { } engine)
                    await TrySelectSubtitleAsync(engine, trackId);
            };

            flyout.Items.Add(item);
        }

        return flyout;
    }

    private MenuFlyout BuildAudioFlyout(IPlaybackTrackController tracks)
    {
        var flyout = new MenuFlyout();

        foreach (var track in tracks.AudioTracks)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = FormatAudioTrack(track),
                GroupName = "audio",
                IsChecked = track.IsSelected
            };

            var trackId = track.Id;
            item.Click += async (_, _) =>
            {
                if (_engine is not { } engine)
                    return;

                try
                {
                    await RunOperationAsync(engine, "audio", async token => await engine.Tracks.SelectAudioTrackAsync(trackId, token), latest: true);
                }
                catch
                {
                    ShowStatus(T("Status_Error"));
                }
            };

            flyout.Items.Add(item);
        }

        return flyout;
    }

    private async Task TrySelectSubtitleAsync(
        IPlaybackEngine engine,
        int? trackId)
    {
        try
        {
            await RunOperationAsync(engine, "subtitle", async token => await engine.Tracks.SelectSubtitleTrackAsync(trackId, token), latest: true);
        }
        catch
        {
            ShowStatus(T("Status_Error"));
        }
    }

    private void UpdateNavigationAvailability()
    {
        var navigation = _engine?.Navigation;
        var hasChapters =
            navigation is not null &&
            navigation.Chapters.Count > 0;

        PreviousChapterButton.IsEnabled = hasChapters
            ? navigation!.SelectedChapterIndex is > 0
            : _engine is not null &&
              _queueIndex > 0;

        NextChapterButton.IsEnabled = hasChapters
            ? navigation!.SelectedChapterIndex is int selected &&
              selected < navigation.Chapters.Count - 1
            : _engine is not null &&
              _queueIndex >= 0 &&
              _queueIndex < _queueItems.Count - 1;

        var previousLabel = hasChapters
            ? T("Playback_PreviousChapter")
            : T("Playback_PreviousEpisode");
        var nextLabel = hasChapters
            ? T("Playback_NextChapter")
            : T("Playback_NextEpisode");

        ToolTipService.SetToolTip(
            PreviousChapterButton,
            previousLabel);
        ToolTipService.SetToolTip(
            NextChapterButton,
            nextLabel);
        AutomationProperties.SetName(
            PreviousChapterButton,
            previousLabel);
        AutomationProperties.SetName(
            NextChapterButton,
            nextLabel);
    }

    private void UpdateDiagnosticsUi(PlaybackDiagnosticsSnapshot snapshot)
    {
        if (_isLoadingStatusVisible)
            UpdateLoadingMetrics(snapshot);
    }

    private void UpdateStateUi(PlaybackState state)
    {
        PlayPauseIcon.Glyph = state == PlaybackState.Playing
            ? ""
            : "";

        var playPauseLabel = state == PlaybackState.Playing
            ? T("Common_Pause")
            : T("Common_Play");

        ToolTipService.SetToolTip(PlayPauseButton, playPauseLabel);
        AutomationProperties.SetName(PlayPauseButton, playPauseLabel);

        switch (state)
        {
            case PlaybackState.Opening:
            case PlaybackState.Buffering:
            case PlaybackState.Seeking:
                ShowLoadingStatus();
                break;

            case PlaybackState.Playing:
            case PlaybackState.Paused:
                HideStatus();
                break;

            case PlaybackState.Ended:
                ShowStatus(T("Playback_Ended"));
                break;

            case PlaybackState.Failed:
                ShowStatus(T("Status_Error"));
                break;

            case PlaybackState.Idle:
            case PlaybackState.Stopped:
                if (_currentSource is null)
                    ShowStatus(T("Playback_NoMediaSource"));
                else
                    HideStatus();
                break;
        }

        if (_isVideoFullscreen)
        {
            if (state == PlaybackState.Playing)
                ShowFullscreenControls(restartAutoHide: true);
            else
                ShowFullscreenControls(restartAutoHide: false);
        }

        UpdateControlAvailability();
    }

    private void UpdateControlAvailability()
    {
        var hasEngineAndSource = _engine is not null && _currentSource is not null;

        PlayPauseButton.IsEnabled = hasEngineAndSource;
        PreviousJumpButton.IsEnabled = hasEngineAndSource;
        NextJumpButton.IsEnabled = hasEngineAndSource;
        PlaybackSlider.IsEnabled = hasEngineAndSource;
        PlaybackRateButton.IsEnabled = hasEngineAndSource;
        VolumeButton.IsEnabled = hasEngineAndSource;

        SubtitleTrackCombo.IsEnabled = hasEngineAndSource;
        SecondarySubtitleCombo.IsEnabled =
            hasEngineAndSource &&
            _externalSubtitles.Any(candidate =>
                candidate.Uri != _primarySubtitleUri);
        AudioTrackCombo.IsEnabled = hasEngineAndSource;

        UpdateNavigationAvailability();
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e) =>
        SetVideoFullscreen(!_isVideoFullscreen);

    private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isVideoFullscreen)
            _sidebarVisibleInFullscreen = !_sidebarVisibleInFullscreen;
        else
            _sidebarCollapsedByUser = !_sidebarCollapsedByUser;

        UpdateSidebarVisibility();
    }

    private void SidebarDismissLayer_PointerPressed(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (!_isVideoFullscreen || !PlayerSplitView.IsPaneOpen)
            return;

        e.Handled = true;
        _sidebarVisibleInFullscreen = false;
        UpdateSidebarVisibility();
    }

    private void PlaybackRateSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var rate = SliderValueToRate(e.NewValue);

        if (PlaybackRateValueText is not null)
            PlaybackRateValueText.Text = $"{rate:0.00}×";

        if (PlaybackRateButton is not null)
            PlaybackRateButton.Content = $"{rate:0.##}×";

        if (_engine is not { } engine)
            return;

        try
        {
            engine.PlaybackRate = rate;
        }
        catch
        {
            ShowStatus(T("Status_Error"));
        }
    }

    private static double SliderValueToRate(double sliderValue)
    {
        var normalized = Math.Clamp(sliderValue, 0d, 1d);
        var rate = normalized <= 0.5d
            ? 0.5d + normalized
            : normalized * 2d;

        return Math.Clamp(
            Math.Round(rate * 20d) / 20d,
            0.5d,
            2.0d);
    }

    private static double RateToSliderValue(double rate)
    {
        var normalizedRate = Math.Clamp(rate, 0.5d, 2.0d);
        return normalizedRate <= 1d
            ? normalizedRate - 0.5d
            : normalizedRate / 2d;
    }

    private void VolumeSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingVolume)
            return;

        SetVolume(e.NewValue / 100d);
    }

    private void SetVolume(double value)
    {
        _volume = Math.Clamp(value, 0d, 1d);

        if (_engine is { } engine)
        {
            try
            {
                engine.Volume = _volume;
            }
            catch
            {
                ShowStatus(T("Status_Error"));
            }
        }

        UpdateVolumeUi(_volume);
    }

    private void UpdateVolumeUi(double value)
    {
        var percent = (int)Math.Round(
            Math.Clamp(value, 0d, 1d) * 100d,
            MidpointRounding.AwayFromZero);

        _isUpdatingVolume = true;
        try
        {
            VolumeSlider.Value = percent;
            VolumeValueText.Text = $"{percent}%";
            VolumeButtonValueText.Text = $"{percent}%";
        }
        finally
        {
            _isUpdatingVolume = false;
        }
    }

    private void InitializeSubtitlePositionControls()
    {
        var settings = AppSettingsStore.Current;

        _primarySubtitleVerticalPosition =
            Math.Clamp(
                settings.PrimarySubtitleVerticalPosition,
                0d,
                90d);
        _secondarySubtitleVerticalPosition =
            Math.Clamp(
                settings.SecondarySubtitleVerticalPosition,
                0d,
                90d);

        _isUpdatingSubtitlePositions = true;
        try
        {
            PrimarySubtitlePositionSlider.Value =
                _primarySubtitleVerticalPosition;
            SecondarySubtitlePositionSlider.Value =
                _secondarySubtitleVerticalPosition;
            UpdateSubtitlePositionValueText();
        }
        finally
        {
            _isUpdatingSubtitlePositions = false;
        }
    }

    private void PrimarySubtitlePositionSlider_ValueChanged(
        object sender,
        RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSubtitlePositions)
            return;

        _primarySubtitleVerticalPosition =
            Math.Clamp(e.NewValue, 0d, 90d);
        UpdateSubtitlePositionValueText();
        ApplySubtitlePositions();

        AppSettingsStore.Update(settings =>
            settings with
            {
                PrimarySubtitleVerticalPosition =
                    _primarySubtitleVerticalPosition
            });
    }

    private void SecondarySubtitlePositionSlider_ValueChanged(
        object sender,
        RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSubtitlePositions)
            return;

        _secondarySubtitleVerticalPosition =
            Math.Clamp(e.NewValue, 0d, 90d);
        UpdateSubtitlePositionValueText();
        ApplySubtitlePositions();

        AppSettingsStore.Update(settings =>
            settings with
            {
                SecondarySubtitleVerticalPosition =
                    _secondarySubtitleVerticalPosition
            });
    }

    private void UpdateSubtitlePositionValueText()
    {
        if (PrimarySubtitlePositionValueText is not null)
        {
            PrimarySubtitlePositionValueText.Text =
                $"{Math.Round(_primarySubtitleVerticalPosition):0}%";
        }

        if (SecondarySubtitlePositionValueText is not null)
        {
            SecondarySubtitlePositionValueText.Text =
                $"{Math.Round(_secondarySubtitleVerticalPosition):0}%";
        }
    }

    private void ApplySubtitlePositions()
    {
        if (PlaybackSurfaceHost is null ||
            PrimarySubtitleOverlay is null ||
            SecondarySubtitleOverlay is null)
        {
            return;
        }

        var height = PlaybackSurfaceHost.ActualHeight;
        if (height <= 0d)
            return;

        ApplySubtitlePosition(
            PrimarySubtitleOverlay,
            _primarySubtitleVerticalPosition,
            height);

        ApplySubtitlePosition(
            SecondarySubtitleOverlay,
            _secondarySubtitleVerticalPosition,
            height);
    }

    private static void ApplySubtitlePosition(
        FrameworkElement overlay,
        double percentage,
        double surfaceHeight)
    {
        var horizontalMargin = 36d;
        var safeBottom = 12d;
        var desiredBottom =
            surfaceHeight *
            Math.Clamp(percentage, 0d, 90d) /
            100d;

        var maxBottom = Math.Max(
            safeBottom,
            surfaceHeight -
            Math.Max(overlay.ActualHeight, 48d) -
            safeBottom);

        var bottom = Math.Clamp(
            desiredBottom,
            safeBottom,
            maxBottom);

        overlay.Margin =
            new Thickness(
                horizontalMargin,
                0d,
                horizontalMargin,
                bottom);
    }

    private void PlayerRoot_PointerWheelChanged(
        object sender,
        PointerRoutedEventArgs e)
    {
        if (!_isVideoFullscreen ||
            _engine is null ||
            _currentSource is null)
        {
            return;
        }

        var delta = e.GetCurrentPoint(PlayerRoot).Properties.MouseWheelDelta;
        if (delta == 0)
            return;

        SetVolume(_volume + (Math.Sign(delta) * 0.05d));
        ShowFullscreenControls(restartAutoHide: true);
        e.Handled = true;
    }

    private void PlayerView_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_pointerWheelHooked)
        {
            _pointerWheelHandler ??= PlayerRoot_PointerWheelChanged;
            PlayerRoot.AddHandler(
                UIElement.PointerWheelChangedEvent,
                _pointerWheelHandler,
                true);
            _pointerWheelHooked = true;
        }

        UpdateSidebarVisibility();
        ApplySubtitlePositions();
    }

    private void PlayerView_Unloaded(object sender, RoutedEventArgs e)
    {
        _fullscreenControlsTimer.Stop();
        _loadingMetricsTimer.Stop();
        RemovePointerWheelHandler();

        if (_isVideoFullscreen)
        {
            _isVideoFullscreen = false;
            _sidebarVisibleInFullscreen = false;
            _hasPointerPosition = false;
            QueueWindowFullscreenExit(++_fullscreenGeneration);
        }
    }

    private void PlayerRoot_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        UpdateSidebarVisibility();
        ApplySubtitlePositions();
    }

    private void PlaybackSurfaceHost_SizeChanged(
        object sender,
        SizeChangedEventArgs e) =>
        ApplySubtitlePositions();

    private void PlayerRoot_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isVideoFullscreen)
            return;

        var position = e.GetCurrentPoint(PlayerRoot).Position;
        var moved =
            !_hasPointerPosition ||
            Math.Abs(position.X - _lastPointerPosition.X) >= 3d ||
            Math.Abs(position.Y - _lastPointerPosition.Y) >= 3d;

        if (!moved)
            return;

        _hasPointerPosition = true;
        _lastPointerPosition = position;
        ShowFullscreenControls(restartAutoHide: true);
    }

    private void PlayerControlsPanel_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_isVideoFullscreen)
            ShowFullscreenControls(restartAutoHide: true);
    }

    private void PlayerControlsPanel_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_isVideoFullscreen)
            RestartFullscreenAutoHide();
    }

    private void PlayerView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isVideoFullscreen || e.Key != VirtualKey.Escape)
            return;

        SetVideoFullscreen(false);
        e.Handled = true;
    }

    private void FullscreenControlsTimer_Tick(object? sender, object e)
    {
        _fullscreenControlsTimer.Stop();

        if (!_isVideoFullscreen ||
            _engine?.State != PlaybackState.Playing)
        {
            return;
        }

        PlayerControlsPanel.Opacity = 0d;
        PlayerControlsPanel.IsHitTestVisible = false;
    }

    private void SetVideoFullscreen(bool enabled)
    {
        if (_isPreparingForDetach || _isVideoFullscreen == enabled) return;
        _isVideoFullscreen = enabled;
        var generation = ++_fullscreenGeneration;
        PlaybackTrace.Write("view", "fullscreen", "requested", enabled.ToString());
        QueueWindowFullscreenExit(generation);
    }

    private void ApplyFullscreenVisualState()
    {
        PlayerRoot.RequestedTheme = ElementTheme.Dark;

        PlayerHeader.Visibility = Visibility.Collapsed;
        HeaderRow.Height = new GridLength(0);
        ControlsRow.Height = new GridLength(0);

        PlayerFrame.Margin = new Thickness(0);
        PlayerFrame.CornerRadius = new CornerRadius(0);

        Grid.SetRow(PlaybackSurfaceHost, 0);
        Grid.SetRowSpan(PlaybackSurfaceHost, 3);

        Grid.SetRow(PlayerControlsPanel, 0);
        Grid.SetRowSpan(PlayerControlsPanel, 3);
        PlayerControlsPanel.VerticalAlignment = VerticalAlignment.Bottom;
        PlayerControlsPanel.Padding = new Thickness(24, 12, 24, 16);
        PlayerControlsPanel.Background =
            new SolidColorBrush(Windows.UI.Color.FromArgb(0xB8, 0, 0, 0));

        _sidebarVisibleInFullscreen = false;
        _hasPointerPosition = false;

        PlayerSplitView.DisplayMode = SplitViewDisplayMode.Overlay;
        PlayerSidebar.Margin = new Thickness(0);
        UpdateSidebarVisibility();

        FullscreenIcon.Glyph = "\uE73F";
        ToolTipService.SetToolTip(
            FullscreenButton,
            T("Playback_ExitFullScreen"));
        AutomationProperties.SetName(
            FullscreenButton,
            T("Playback_ExitFullScreen"));

        ShowFullscreenControls(restartAutoHide: true);
        Focus(FocusState.Programmatic);
    }

    private void ApplyWindowedVisualState()
    {
        _fullscreenControlsTimer.Stop();
        _sidebarVisibleInFullscreen = false;
        _hasPointerPosition = false;

        PlayerRoot.RequestedTheme = ElementTheme.Default;

        PlayerHeader.Visibility = Visibility.Visible;
        HeaderRow.Height = new GridLength(64);
        ControlsRow.Height = new GridLength(108);

        PlayerFrame.Margin = new Thickness(16);
        PlayerFrame.CornerRadius = new CornerRadius(8);

        Grid.SetRow(PlaybackSurfaceHost, 1);
        Grid.SetRowSpan(PlaybackSurfaceHost, 1);

        Grid.SetRow(PlayerControlsPanel, 2);
        Grid.SetRowSpan(PlayerControlsPanel, 1);
        PlayerControlsPanel.VerticalAlignment = VerticalAlignment.Stretch;
        PlayerControlsPanel.Padding = new Thickness(20, 10, 20, 10);
        PlayerControlsPanel.Background =
            new SolidColorBrush(Colors.Transparent);
        PlayerControlsPanel.Opacity = 1d;
        PlayerControlsPanel.IsHitTestVisible = true;

        PlayerSplitView.DisplayMode = SplitViewDisplayMode.Inline;
        PlayerSidebar.Margin = new Thickness(0);
        UpdateSidebarVisibility();

        FullscreenIcon.Glyph = "\uE740";
        ToolTipService.SetToolTip(
            FullscreenButton,
            T("Playback_FullScreen"));
        AutomationProperties.SetName(
            FullscreenButton,
            T("Playback_FullScreen"));
    }

    private void UpdatePlaybackControlLayout()
    {
        var sidebarWidth =
            !_isVideoFullscreen && PlayerSplitView.IsPaneOpen
                ? PlayerSplitView.OpenPaneLength
                : 0d;

        var availableWidth = Math.Max(
            0d,
            PlayerRoot.ActualWidth - sidebarWidth - 32d);

        if (_isVideoFullscreen)
        {
            LeftPlaybackColumn.Width = new GridLength(1d, GridUnitType.Star);
            CenterPlaybackColumn.Width = GridLength.Auto;
            RightPlaybackColumn.Width = new GridLength(1d, GridUnitType.Star);

            Grid.SetRow(CenterPlaybackControls, 0);
            Grid.SetColumn(CenterPlaybackControls, 1);
            Grid.SetColumnSpan(CenterPlaybackControls, 1);
            CenterPlaybackControls.Margin = new Thickness(0);

            return;
        }

        LeftPlaybackColumn.Width = GridLength.Auto;
        CenterPlaybackColumn.Width = new GridLength(1d, GridUnitType.Star);
        RightPlaybackColumn.Width = GridLength.Auto;

        var compact = availableWidth < 760d;
        if (compact)
        {
            ControlsRow.Height = new GridLength(150d);

            Grid.SetRow(CenterPlaybackControls, 1);
            Grid.SetColumn(CenterPlaybackControls, 0);
            Grid.SetColumnSpan(CenterPlaybackControls, 3);
            CenterPlaybackControls.Margin = new Thickness(0, 6, 0, 0);

            return;
        }

        ControlsRow.Height = new GridLength(108d);

        Grid.SetRow(CenterPlaybackControls, 0);
        Grid.SetColumn(CenterPlaybackControls, 1);
        Grid.SetColumnSpan(CenterPlaybackControls, 1);
        CenterPlaybackControls.Margin = new Thickness(0);

    }

    private void UpdateSidebarVisibility()
    {
        var shouldShow = _isVideoFullscreen
            ? _sidebarVisibleInFullscreen
            : !_sidebarCollapsedByUser && PlayerRoot.ActualWidth >= 900d;

        PlayerSplitView.DisplayMode = _isVideoFullscreen
            ? SplitViewDisplayMode.Overlay
            : SplitViewDisplayMode.Inline;

        PlayerSplitView.IsPaneOpen = shouldShow;
        SidebarDismissLayer.Visibility =
            _isVideoFullscreen && shouldShow
                ? Visibility.Visible
                : Visibility.Collapsed;

        SidebarToggleIcon.Symbol = shouldShow
            ? Symbol.ClosePane
            : Symbol.OpenPane;

        var label = shouldShow
            ? T("Playback_CollapseSidebar")
            : T("Playback_ShowSidebar");

        ToolTipService.SetToolTip(SidebarToggleButton, label);
        AutomationProperties.SetName(SidebarToggleButton, label);

        UpdatePlaybackControlLayout();
    }

    private void ShowFullscreenControls(bool restartAutoHide)
    {
        PlayerControlsPanel.Opacity = 1d;
        PlayerControlsPanel.IsHitTestVisible = true;

        if (!_isVideoFullscreen)
            return;

        if (restartAutoHide)
            RestartFullscreenAutoHide();
        else
            _fullscreenControlsTimer.Stop();
    }

    private void RestartFullscreenAutoHide()
    {
        _fullscreenControlsTimer.Stop();

        if (_isVideoFullscreen &&
            _engine?.State == PlaybackState.Playing)
        {
            _fullscreenControlsTimer.Start();
        }
    }

    private void RemovePointerWheelHandler()
    {
        if (!_pointerWheelHooked || _pointerWheelHandler is null)
            return;

        PlayerRoot.RemoveHandler(
            UIElement.PointerWheelChangedEvent,
            _pointerWheelHandler);
        _pointerWheelHooked = false;
    }

    private void QueueWindowFullscreenExit(int generation)
    {
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            // Only the most recent fullscreen intent may mutate the window presenter.
            // Stale queued callbacks (tab closed, unloaded, rapid toggling) become no-ops.
            if (generation != Volatile.Read(ref _fullscreenGeneration) || _isPreparingForDetach)
            {
                PlaybackTrace.Write("view", "fullscreen", "stale");
                return;
            }

            var enabled = _isVideoFullscreen;
            try
            {
                if (enabled) ApplyFullscreenVisualState();
                else ApplyWindowedVisualState();
                App.MainWindow?.SetPlayerVideoFullscreen(enabled, this);
                PlaybackTrace.Write("view", "fullscreen", "complete", enabled.ToString());
            }
            catch (Exception exception)
            {
                PlaybackTrace.Write("view", "fullscreen", "error", exception.GetType().Name + ":" + exception.Message);
                // Never leave the window presenter out of sync with the player state;
                // a half-applied transition is what makes fullscreen unrecoverable.
                _isVideoFullscreen = false;
                _fullscreenGeneration++;
                try
                {
                    App.MainWindow?.SetPlayerVideoFullscreen(false, this);
                }
                catch (Exception restoreException)
                {
                    PlaybackTrace.Write("view", "fullscreen", "restore-error", restoreException.GetType().Name);
                }
                ShowStatus(T("Status_Error"));
            }
        }))
        {
            PlaybackTrace.Write("view", "fullscreen", "dispatcher-closed");
        }
    }

    internal ValueTask PrepareForDetachAsync() => new(_detachTask ??= PrepareForDetachCoreAsync());

    private async Task PrepareForDetachCoreAsync()
    {
        if (_isPreparingForDetach)
            return;

        await _surfaceLifecycleGate.WaitAsync();
        try
        {
            if (_isPreparingForDetach)
                return;

            _isPreparingForDetach = true;
            _session.Cancel();
            PlaybackTrace.Write("view", "detach", "start");
            _fullscreenControlsTimer.Stop();
            _loadingMetricsTimer.Stop();
            RemovePointerWheelHandler();

            _seekDebounce?.Cancel();
            _seekDebounce = null;

            _isVideoFullscreen = false;
            _sidebarVisibleInFullscreen = false;
            _hasPointerPosition = false;
            _fullscreenGeneration++;

            App.MainWindow?.SetPlayerVideoFullscreen(false, this);
            if (_engine is { } engine)
            {
                DetachEngine(engine);
                _engine = null;
            }

            PlaybackSurface.EngineChanged -= PlaybackSurface_EngineChanged;
            PlaybackSurface.InitializationFailed -= PlaybackSurface_InitializationFailed;
        }
        finally
        {
            _surfaceLifecycleGate.Release();
        }

        try
        {
            await PlaybackSurface.DisposeAsync();
        }
        catch (Exception exception)
        { PlaybackTrace.Write("view", "detach", "error", exception.GetType().Name); }
        PlaybackTrace.Write("view", "detach", "complete");
    }

    private void ResetTimeline()
    {
        _isUpdatingTimeline = true;
        try
        {
            PlaybackSlider.Minimum = 0;
            PlaybackSlider.Maximum = 1;
            PlaybackSlider.Value = 0;
            CurrentTimeText.Text = FormatTime(TimeSpan.Zero);
            DurationText.Text = FormatTime(TimeSpan.Zero);
            UpdateSecondarySubtitle(TimeSpan.Zero);
        }
        finally
        {
            _isUpdatingTimeline = false;
        }
    }

    private void ShowStatus(string text)
    {
        _isLoadingStatusVisible = false;
        _loadingMetricsTimer.Stop();
        PlaybackLoadingRing.Visibility = Visibility.Collapsed;
        PlaybackLoadingMetricsText.Visibility = Visibility.Collapsed;
        PlaybackStatusText.Text = text;
        PlaybackStatusPanel.Visibility = Visibility.Visible;
    }

    private void ShowLoadingStatus()
    {
        PlaybackStatusText.Text = T("Status_Loading");
        PlaybackStatusPanel.Visibility = Visibility.Visible;
        PlaybackLoadingRing.Visibility = Visibility.Visible;
        PlaybackLoadingMetricsText.Visibility = Visibility.Visible;

        if (!_isLoadingStatusVisible)
        {
            _isLoadingStatusVisible = true;
            _lastLoadingReadBytes = null;
            _lastLoadingSampleAt = null;
            PlaybackLoadingRing.IsIndeterminate = true;
            PlaybackLoadingRing.Value = 0d;
            PlaybackLoadingMetricsText.Text =
                string.Format(T("Playback_LoadingProgressFormat"), 0d);
        }

        if (!_loadingMetricsTimer.IsEnabled)
            _loadingMetricsTimer.Start();
    }

    private void HideStatus()
    {
        _isLoadingStatusVisible = false;
        _loadingMetricsTimer.Stop();
        PlaybackStatusPanel.Visibility = Visibility.Collapsed;
    }

    private async void LoadingMetricsTimer_Tick(object? sender, object e)
    {
        if (_loadingMetricsRefreshInFlight ||
            !_isLoadingStatusVisible ||
            _engine is not { } engine)
        {
            return;
        }

        _loadingMetricsRefreshInFlight = true;

        try
        {
            var snapshot =
                await engine.Diagnostics.RefreshAsync(_session.Token);
            if (ReferenceEquals(_engine, engine) && !_isPreparingForDetach) UpdateLoadingMetrics(snapshot);
        }
        catch
        {
            // Loading metrics are observational and must never interrupt playback.
        }
        finally
        {
            _loadingMetricsRefreshInFlight = false;
        }
    }

    private void UpdateLoadingMetrics(PlaybackDiagnosticsSnapshot snapshot)
    {
        var percent = Math.Clamp(
            snapshot.Capabilities.BufferingPercent,
            0d,
            100d);

        if (percent > 0d)
        {
            PlaybackLoadingRing.IsIndeterminate = false;
            PlaybackLoadingRing.Value = percent;
        }
        else
        {
            PlaybackLoadingRing.IsIndeterminate = true;
        }

        var now = snapshot.CapturedAt;
        var readBytes = snapshot.Statistics?.ReadBytes;
        double? megabytesPerSecond = null;

        if (readBytes is long currentBytes &&
            _lastLoadingReadBytes is long previousBytes &&
            _lastLoadingSampleAt is DateTimeOffset previousAt)
        {
            var elapsed = (now - previousAt).TotalSeconds;
            var delta = currentBytes - previousBytes;

            if (elapsed > 0.05d && delta >= 0)
            {
                megabytesPerSecond =
                    delta / elapsed / (1024d * 1024d);
            }
        }

        if (readBytes is long bytes)
        {
            _lastLoadingReadBytes = bytes;
            _lastLoadingSampleAt = now;
        }

        var progressText =
            string.Format(
                T("Playback_LoadingProgressFormat"),
                percent);

        if (_currentSource is { Uri.IsFile: false } &&
            megabytesPerSecond is double speed &&
            double.IsFinite(speed))
        {
            PlaybackLoadingMetricsText.Text =
                progressText +
                " · " +
                string.Format(
                    T("Playback_LoadingSpeedFormat"),
                    speed);
        }
        else
        {
            PlaybackLoadingMetricsText.Text =
                progressText;
        }
    }

    private async void QueueList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isUpdatingQueueSelection ||
            QueueList.SelectedIndex < 0 ||
            QueueList.SelectedIndex == _queueIndex)
        {
            return;
        }

        await SwitchQueueItemAsync(
            QueueList.SelectedIndex,
            autoplay: true);
    }

    private async Task SwitchQueueItemAsync(
        int index,
        bool autoplay)
    {
        if (index < 0 ||
            index >= _queueItems.Count ||
            index == _queueIndex &&
            _currentSource == _queueItems[index].Source)
        {
            return;
        }

        var item = _queueItems[index];

        _queueIndex = index;
        _currentSource = item.Source;
        PlaybackSelectionTrace.Write(
            "queue-switch",
            item.CatalogItem?.SourceTitle ?? item.Title,
            item.Source.Uri.ToString(),
            _queueIndex,
            _queueItems.Count);
        _lastKnownPosition = TimeSpan.Zero;
        _duration = TimeSpan.Zero;
        _playIntent = autoplay;

        NowPlayingEpisode.Text = item.Title;
        QueueSubtitle.Text = item.Title;

        ResetTimeline();
        ResetSecondarySubtitleState();
        RefreshQueueStatus();
        UpdateControlAvailability();

        if (_engine is not { } engine)
            return;

        ShowLoadingStatus();
        await OpenSourceOnEngineAsync(
            engine,
            restorePosition: false,
            latest: true);
    }

    private void RefreshQueueStatus()
    {
        for (var index = 0; index < _queueItems.Count; index++)
        {
            var item = _queueItems[index];
            item.Status = index == _queueIndex
                ? string.IsNullOrWhiteSpace(item.SourceLabel)
                    ? T("Playback_Playing")
                    : T("Playback_Playing") + " · " + item.SourceLabel
                : item.SourceLabel;
        }

        _isUpdatingQueueSelection = true;
        try
        {
            QueueList.SelectedIndex =
                _queueIndex >= 0 &&
                _queueIndex < _queueItems.Count
                    ? _queueIndex
                    : -1;
        }
        finally
        {
            _isUpdatingQueueSelection = false;
        }

        if (CurrentQueueItem is { } current)
        {
            QueueSubtitle.Text = current.Title;

            if (QueueList.IsLoaded)
                QueueList.ScrollIntoView(current);
        }

        UpdateNavigationAvailability();
    }

    private void PlayerSectionList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        UpdateSidebarSectionUi();

    private void UpdateSidebarSectionUi()
    {
        if (QueueList is null ||
            TracksPanel is null ||
            PlayerSectionList is null)
        {
            return;
        }

        var showTracks =
            PlayerSectionList.SelectedIndex == 1;

        QueueList.Visibility = showTracks
            ? Visibility.Collapsed
            : Visibility.Visible;
        TracksPanel.Visibility = showTracks
            ? Visibility.Visible
            : Visibility.Collapsed;

        QueueTitle.Text = showTracks
            ? T("Playback_Tracks")
            : T("Playback_Queue");
        QueueSubtitle.Visibility = showTracks
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Dispatch(Action action)
    {
        var session = _session;

        void Invoke()
        {
            if (_isPreparingForDetach ||
                !ReferenceEquals(session, _session) ||
                session.Token.IsCancellationRequested)
            {
                return;
            }

            action();
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            Invoke();
            return;
        }

        DispatcherQueue.TryEnqueue(Invoke);
    }

    private static string FormatSubtitleTrack(SubtitleTrackInfo track)
    {
        var parts = new[]
        {
            track.Name,
            track.Language,
            track.Codec
        }.Where(static value => !string.IsNullOrWhiteSpace(value));

        var text = string.Join(" · ", parts);
        return string.IsNullOrWhiteSpace(text)
            ? $"#{track.Id}"
            : text;
    }

    private static string FormatAudioTrack(AudioTrackInfo track)
    {
        var channels = track.Channels is int channelCount
            ? $"{channelCount}ch"
            : null;

        var parts = new[]
        {
            track.Name,
            track.Language,
            track.Codec,
            channels
        }.Where(static value => !string.IsNullOrWhiteSpace(value));

        var text = string.Join(" · ", parts);
        return string.IsNullOrWhiteSpace(text)
            ? $"#{track.Id}"
            : text;
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            value = TimeSpan.Zero;

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
    }
}
