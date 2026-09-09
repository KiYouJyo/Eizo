using Eizo.Localization;
using Eizo.Models;
using Eizo.Playback;
using Eizo.Playback.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.System;

namespace Eizo.Views;

public sealed partial class PlayerView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly SemaphoreSlim _sourceGate = new(1, 1);
    private readonly DispatcherTimer _fullscreenControlsTimer;

    private IPlaybackEngine? _engine;
    private PlaybackSource? _currentSource;
    private TimeSpan _lastKnownPosition;
    private TimeSpan _duration;
    private bool _playIntent;
    private bool _isUpdatingTimeline;
    private bool _isUpdatingTrackSelections;
    private bool _isVideoFullscreen;
    private bool _sidebarCollapsedByUser;
    private bool _sidebarVisibleInFullscreen;
    private bool _pointerOverControls;
    private bool _hasPointerPosition;
    private Point _lastPointerPosition;
    private DateTime _lastPointerActivityUtc = DateTime.UtcNow;
    private int _sidebarAnimationVersion;
    private CancellationTokenSource? _seekDebounce;

    public PlayerView(string title, string episode)
    {
        InitializeComponent();

        NowPlayingTitle.Text = title + " · " + episode;
        NowPlayingEpisode.Text = "一级魔法使考试";

        QueueList.ItemsSource = new[]
        {
            new EpisodeItemModel("18", "一级魔法使考试", "一級魔法使試験", "23:41", T("Playback_Playing")),
            new EpisodeItemModel("19", "周密的计划", "入念な計画", "24:03", T("Category_Unwatched")),
            new EpisodeItemModel("20", "必要的杀戮", "必要な殺し", "23:58", T("Category_Unwatched")),
            new EpisodeItemModel("21", "魔法的世界", "魔法の世界", "24:11", T("Category_Unwatched"))
        };

        PlaybackSurface.EngineChanged += PlaybackSurface_EngineChanged;
        PlaybackSurface.InitializationFailed += PlaybackSurface_InitializationFailed;

        _fullscreenControlsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _fullscreenControlsTimer.Tick += FullscreenControlsTimer_Tick;

        Loaded += PlayerView_Loaded;
        Unloaded += PlayerView_Unloaded;

        ApplyText();
        ResetTimeline();
        ShowStatus(T("Playback_SelectLocalMedia"));
        UpdateControlAvailability();
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        SubtitleQuickButton.Content = T("Playback_SubtitleTrack");
        AudioQuickButton.Content = T("Playback_AudioTrack");
        QueueTitle.Text = T("Playback_Queue");
        QueueSubtitle.Text = T("Section_Anime");
        PlaybackInfoTitle.Text = T("Playback_Info");
        PlaybackRateFlyoutTitle.Text = T("Playback_Rate");

        PlayerSectionList.ItemsSource = new[]
        {
            T("Playback_Queue"),
            T("Playback_Tracks")
        };

        ToolTipService.SetToolTip(OpenMediaButton, T("Playback_OpenLocalMedia"));
        ToolTipService.SetToolTip(PreviousChapterButton, T("Playback_PreviousChapter"));
        ToolTipService.SetToolTip(PreviousJumpButton, T("Playback_Back10Seconds"));
        ToolTipService.SetToolTip(PlayPauseButton, T("Common_Play"));
        ToolTipService.SetToolTip(NextJumpButton, T("Playback_Forward10Seconds"));
        ToolTipService.SetToolTip(NextChapterButton, T("Playback_NextChapter"));
        ToolTipService.SetToolTip(SidebarToggleButton, T("Playback_CollapseSidebar"));
        ToolTipService.SetToolTip(FullscreenButton, T("Playback_FullScreen"));

        AutomationProperties.SetName(OpenMediaButton, T("Playback_OpenLocalMedia"));
        AutomationProperties.SetName(PreviousChapterButton, T("Playback_PreviousChapter"));
        AutomationProperties.SetName(PreviousJumpButton, T("Playback_Back10Seconds"));
        AutomationProperties.SetName(PlayPauseButton, T("Common_Play"));
        AutomationProperties.SetName(NextJumpButton, T("Playback_Forward10Seconds"));
        AutomationProperties.SetName(NextChapterButton, T("Playback_NextChapter"));
        AutomationProperties.SetName(SidebarToggleButton, T("Playback_CollapseSidebar"));
        AutomationProperties.SetName(FullscreenButton, T("Playback_FullScreen"));
    }

    private async void PlaybackSurface_EngineChanged(
        object? sender,
        PlaybackViewEngineChangedEventArgs e)
    {
        if (e.PreviousEngine is not null)
            DetachEngine(e.PreviousEngine);

        _engine = e.CurrentEngine;

        if (e.CurrentEngine is null)
        {
            UpdateControlAvailability();
            return;
        }

        AttachEngine(e.CurrentEngine);
        UpdateControlAvailability();

        if (_currentSource is null)
        {
            ShowStatus(T("Playback_SelectLocalMedia"));
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

        Dispatch(() =>
        {
            UpdateStateUi(engine.State);
            UpdateTrackUi();
            UpdateNavigationAvailability();
            UpdateDiagnosticsUi(engine.Diagnostics.Current);
            PlaybackRateSlider.Value = Math.Clamp(engine.PlaybackRate, 0.5d, 2.0d);
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

    private void Engine_StateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        if (e.CurrentState == PlaybackState.Ended)
            _playIntent = false;

        Dispatch(() => UpdateStateUi(e.CurrentState));
    }

    private void Engine_PositionChanged(object? sender, PlaybackPositionChangedEventArgs e)
    {
        _lastKnownPosition = e.Position;

        Dispatch(() =>
        {
            CurrentTimeText.Text = FormatTime(e.Position);

            if (_duration <= TimeSpan.Zero)
                return;

            _isUpdatingTimeline = true;
            try
            {
                PlaybackSlider.Value = Math.Clamp(
                    e.Position.TotalSeconds,
                    PlaybackSlider.Minimum,
                    PlaybackSlider.Maximum);
            }
            finally
            {
                _isUpdatingTimeline = false;
            }
        });
    }

    private void Engine_DurationChanged(object? sender, PlaybackDurationChangedEventArgs e)
    {
        _duration = e.Duration;

        Dispatch(() =>
        {
            _isUpdatingTimeline = true;
            try
            {
                PlaybackSlider.Maximum = Math.Max(1d, e.Duration.TotalSeconds);
                DurationText.Text = FormatTime(e.Duration);
            }
            finally
            {
                _isUpdatingTimeline = false;
            }
        });
    }

    private void Engine_Failed(object? sender, PlaybackFailedEventArgs e)
    {
        _playIntent = false;
        Dispatch(() => ShowStatus(T("Status_Error")));
    }

    private void Tracks_TracksChanged(object? sender, PlaybackTracksChangedEventArgs e) =>
        Dispatch(UpdateTrackUi);

    private void Navigation_NavigationChanged(
        object? sender,
        PlaybackNavigationChangedEventArgs e) =>
        Dispatch(UpdateNavigationAvailability);

    private void Diagnostics_DiagnosticsChanged(
        object? sender,
        PlaybackDiagnosticsChangedEventArgs e) =>
        Dispatch(() => UpdateDiagnosticsUi(e.Snapshot));

    private async void OpenMediaButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };

            foreach (var extension in new[]
            {
                ".mkv", ".mp4", ".m4v", ".mov", ".avi", ".webm",
                ".ts", ".m2ts", ".wmv", ".mpg", ".mpeg",
                ".mp3", ".flac", ".wav", ".m4a", ".ogg", ".opus"
            })
            {
                picker.FileTypeFilter.Add(extension);
            }

            if (App.MainWindow is null)
                return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();

            if (file is null)
                return;

            MediaCatalogStore.Default.RegisterLocalFile(file.Path);

            _currentSource = PlaybackSource.FromFile(file.Path, file.DisplayName);
            _lastKnownPosition = TimeSpan.Zero;
            _duration = TimeSpan.Zero;
            _playIntent = true;

            ResetTimeline();
            ShowStatus(T("Status_Loading"));

            if (_engine is { } engine)
                await OpenSourceOnEngineAsync(engine, restorePosition: false);
        }
        catch
        {
            _playIntent = false;
            ShowStatus(T("Status_Error"));
        }
    }

    private async Task RestoreSourceOnEngineAsync(IPlaybackEngine engine)
    {
        try
        {
            ShowStatus(T("Status_Loading"));
            await OpenSourceOnEngineAsync(engine, restorePosition: true);
        }
        catch
        {
            _playIntent = false;
            ShowStatus(T("Status_Error"));
        }
    }

    private async Task OpenSourceOnEngineAsync(
        IPlaybackEngine engine,
        bool restorePosition)
    {
        if (_currentSource is null)
            return;

        await _sourceGate.WaitAsync();

        try
        {
            if (!ReferenceEquals(_engine, engine))
                return;

            var source = _currentSource;
            var resumePosition = restorePosition
                ? _lastKnownPosition
                : TimeSpan.Zero;
            var shouldPlay = _playIntent;

            await engine.OpenAsync(source);
            await engine.PlayAsync();

            if (resumePosition > TimeSpan.FromMilliseconds(250))
                await TryRestorePositionAsync(engine, resumePosition);

            if (!shouldPlay)
                await engine.PauseAsync();

            await engine.Tracks.RefreshAsync();
            await engine.Navigation.RefreshAsync();
            var diagnostics = await engine.Diagnostics.RefreshAsync();

            Dispatch(() =>
            {
                UpdateTrackUi();
                UpdateNavigationAvailability();
                UpdateDiagnosticsUi(diagnostics);
            });
        }
        finally
        {
            _sourceGate.Release();
        }
    }

    private static async Task TryRestorePositionAsync(
        IPlaybackEngine engine,
        TimeSpan position)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var diagnostics = await engine.Diagnostics.RefreshAsync();

            if (diagnostics.Capabilities.CanSeek)
            {
                await engine.SeekAsync(position);
                return;
            }

            await Task.Delay(50);
        }
    }

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSource is null)
        {
            OpenMediaButton_Click(OpenMediaButton, e);
            return;
        }

        if (_engine is not { } engine)
            return;

        try
        {
            if (engine.State == PlaybackState.Playing)
            {
                _playIntent = false;
                await engine.PauseAsync();
            }
            else
            {
                _playIntent = true;
                await engine.PlayAsync();
            }
        }
        catch
        {
            ShowStatus(T("Status_Error"));
        }
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

            await engine.SeekAsync(target);
        }
        catch
        {
        }
    }

    private async void PreviousChapterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine is not { } engine)
            return;

        try
        {
            await engine.Navigation.PreviousChapterAsync();
        }
        catch
        {
        }
    }

    private async void NextChapterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine is not { } engine)
            return;

        try
        {
            await engine.Navigation.NextChapterAsync();
        }
        catch
        {
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
        _seekDebounce?.Dispose();
        _seekDebounce = new CancellationTokenSource();
        var token = _seekDebounce.Token;
        var target = TimeSpan.FromSeconds(e.NewValue);

        _lastKnownPosition = target;
        CurrentTimeText.Text = FormatTime(target);

        try
        {
            await Task.Delay(80, token);
            await engine.SeekAsync(target, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
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

        try
        {
            await engine.Tracks.SelectSubtitleTrackAsync(
                item.Tag is int id ? id : null);
        }
        catch
        {
            ShowStatus(T("Status_Error"));
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
            await engine.Tracks.SelectAudioTrackAsync(id);
        }
        catch
        {
            ShowStatus(T("Status_Error"));
        }
    }

    private void UpdateTrackUi()
    {
        if (_engine is not { } engine)
            return;

        var tracks = engine.Tracks;

        _isUpdatingTrackSelections = true;
        try
        {
            SubtitleTrackCombo.Items.Clear();

            var offItem = new ComboBoxItem
            {
                Content = T("Playback_NoSubtitle"),
                Tag = null
            };

            SubtitleTrackCombo.Items.Add(offItem);

            ComboBoxItem? selectedSubtitleItem = tracks.SelectedSubtitleTrackId is null
                ? offItem
                : null;

            foreach (var track in tracks.SubtitleTracks)
            {
                var item = new ComboBoxItem
                {
                    Content = FormatSubtitleTrack(track),
                    Tag = track.Id
                };

                SubtitleTrackCombo.Items.Add(item);

                if (tracks.SelectedSubtitleTrackId == track.Id)
                    selectedSubtitleItem = item;
            }

            SubtitleTrackCombo.SelectedItem = selectedSubtitleItem ?? offItem;

            AudioTrackCombo.Items.Clear();
            ComboBoxItem? selectedAudioItem = null;

            foreach (var track in tracks.AudioTracks)
            {
                var item = new ComboBoxItem
                {
                    Content = FormatAudioTrack(track),
                    Tag = track.Id
                };

                AudioTrackCombo.Items.Add(item);

                if (tracks.SelectedAudioTrackId == track.Id)
                    selectedAudioItem = item;
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
                selectedAudioItem ??
                (AudioTrackCombo.Items.Count > 0
                    ? AudioTrackCombo.Items[0]
                    : null);

            SubtitleQuickButton.Content =
                tracks.SubtitleTracks.FirstOrDefault(track => track.IsSelected) is { } subtitle
                    ? FormatSubtitleTrack(subtitle)
                    : T("Playback_SubtitleTrack");

            AudioQuickButton.Content =
                tracks.AudioTracks.FirstOrDefault(track => track.IsSelected) is { } audio
                    ? FormatAudioTrack(audio)
                    : T("Playback_AudioTrack");

            SubtitleQuickButton.Flyout = BuildSubtitleFlyout(tracks);
            AudioQuickButton.Flyout = BuildAudioFlyout(tracks);
        }
        finally
        {
            _isUpdatingTrackSelections = false;
        }
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
                    await engine.Tracks.SelectAudioTrackAsync(trackId);
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
            await engine.Tracks.SelectSubtitleTrackAsync(trackId);
        }
        catch
        {
            ShowStatus(T("Status_Error"));
        }
    }

    private void UpdateNavigationAvailability()
    {
        var navigation = _engine?.Navigation;

        PreviousChapterButton.IsEnabled =
            navigation is not null &&
            navigation.Chapters.Count > 0 &&
            navigation.SelectedChapterIndex is > 0;

        NextChapterButton.IsEnabled =
            navigation is not null &&
            navigation.Chapters.Count > 0 &&
            navigation.SelectedChapterIndex is int selected &&
            selected < navigation.Chapters.Count - 1;
    }

    private void UpdateDiagnosticsUi(PlaybackDiagnosticsSnapshot snapshot)
    {
        var video = snapshot.SelectedVideoTrack;

        VideoInfoText.Text = video is null
            ? "—"
            : string.Join(
                " · ",
                new[]
                {
                    video.Codec,
                    video.Width is int width && video.Height is int height
                        ? $"{width}×{height}"
                        : null,
                    video.FrameRate is double fps
                        ? $"{fps:0.###} FPS"
                        : null
                }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        SourceInfoText.Text = snapshot.InputKind switch
        {
            PlaybackInputKind.LocalFile => T("Source_Local"),
            PlaybackInputKind.Network => snapshot.InputScheme?.ToUpperInvariant() ?? T("Common_Unknown"),
            PlaybackInputKind.OtherUri => snapshot.InputScheme?.ToUpperInvariant() ?? T("Common_Unknown"),
            _ => "—"
        };
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
                ShowStatus(T("Status_Loading"));
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
                    ShowStatus(T("Playback_SelectLocalMedia"));
                else
                    HideStatus();
                break;
        }

        if (_isVideoFullscreen)
        {
            if (state == PlaybackState.Playing)
                ShowFullscreenControls();
            else
                ShowFullscreenControls();
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

        SubtitleQuickButton.IsEnabled = hasEngineAndSource;
        AudioQuickButton.IsEnabled = hasEngineAndSource;
        SubtitleTrackCombo.IsEnabled = hasEngineAndSource;
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

    private void PlaybackRateSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var rate = Math.Clamp(
            Math.Round(e.NewValue * 20d) / 20d,
            0.5d,
            2.0d);

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

    private void PlayerView_Loaded(object sender, RoutedEventArgs e) =>
        UpdateSidebarVisibility();

    private void PlayerView_Unloaded(object sender, RoutedEventArgs e)
    {
        _fullscreenControlsTimer.Stop();

        if (_isVideoFullscreen)
            SetVideoFullscreen(false);
    }

    private void PlayerRoot_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateSidebarVisibility();

    private void PlayerRoot_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isVideoFullscreen)
            return;

        var position = e.GetCurrentPoint(PlayerRoot).Position;
        var moved =
            !_hasPointerPosition ||
            Math.Abs(position.X - _lastPointerPosition.X) >= 0.75d ||
            Math.Abs(position.Y - _lastPointerPosition.Y) >= 0.75d;

        if (!moved)
            return;

        _hasPointerPosition = true;
        _lastPointerPosition = position;
        _lastPointerActivityUtc = DateTime.UtcNow;
        ShowFullscreenControls();
    }

    private void PlayerControlsPanel_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _pointerOverControls = true;
        _lastPointerActivityUtc = DateTime.UtcNow;

        if (_isVideoFullscreen)
            ShowFullscreenControls();
    }

    private void PlayerControlsPanel_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _pointerOverControls = false;
        _lastPointerActivityUtc = DateTime.UtcNow;
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
        if (!_isVideoFullscreen)
        {
            _fullscreenControlsTimer.Stop();
            return;
        }

        if (_engine?.State != PlaybackState.Playing)
        {
            ShowFullscreenControls();
            return;
        }

        if (_pointerOverControls)
            return;

        if (DateTime.UtcNow - _lastPointerActivityUtc < TimeSpan.FromSeconds(2.5))
            return;

        PlayerControlsPanel.Opacity = 0d;
        PlayerControlsPanel.IsHitTestVisible = false;
    }

    private void SetVideoFullscreen(bool enabled)
    {
        if (_isVideoFullscreen == enabled)
            return;

        _isVideoFullscreen = enabled;
        App.MainWindow?.SetPlayerVideoFullscreen(enabled);

        if (enabled)
        {
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
            SidebarColumn.Width = new GridLength(0);
            SidebarToggleButton.Visibility = Visibility.Visible;
            UpdateSidebarVisibility();

            FullscreenIcon.Glyph = "\uE73F";
            ToolTipService.SetToolTip(
                FullscreenButton,
                T("Playback_ExitFullScreen"));
            AutomationProperties.SetName(
                FullscreenButton,
                T("Playback_ExitFullScreen"));

            _lastPointerActivityUtc = DateTime.UtcNow;
            _fullscreenControlsTimer.Start();
            ShowFullscreenControls();
            Focus(FocusState.Programmatic);
            return;
        }

        _fullscreenControlsTimer.Stop();
        _sidebarVisibleInFullscreen = false;

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

        SidebarToggleButton.Visibility = Visibility.Visible;

        FullscreenIcon.Glyph = "\uE740";
        ToolTipService.SetToolTip(
            FullscreenButton,
            T("Playback_FullScreen"));
        AutomationProperties.SetName(
            FullscreenButton,
            T("Playback_FullScreen"));

        UpdateSidebarVisibility();
    }

    private void UpdateSidebarVisibility()
    {
        var shouldShow = _isVideoFullscreen
            ? _sidebarVisibleInFullscreen
            : !_sidebarCollapsedByUser && PlayerRoot.ActualWidth >= 900d;

        if (_isVideoFullscreen)
        {
            Grid.SetColumn(PlayerSidebar, 0);
            PlayerSidebar.HorizontalAlignment = HorizontalAlignment.Right;
            SidebarColumn.Width = new GridLength(0);
            Canvas.SetZIndex(PlayerSidebar, 20);
        }
        else
        {
            Grid.SetColumn(PlayerSidebar, 1);
            PlayerSidebar.HorizontalAlignment = HorizontalAlignment.Stretch;
            Canvas.SetZIndex(PlayerSidebar, 0);

            if (shouldShow || PlayerSidebar.Visibility == Visibility.Visible)
                SidebarColumn.Width = GridLength.Auto;
            else
                SidebarColumn.Width = new GridLength(0);
        }

        _ = AnimateSidebarAsync(shouldShow);

        SidebarToggleIcon.Symbol = shouldShow
            ? Symbol.ClosePane
            : Symbol.OpenPane;

        var label = shouldShow
            ? T("Playback_CollapseSidebar")
            : T("Playback_ShowSidebar");

        ToolTipService.SetToolTip(SidebarToggleButton, label);
        AutomationProperties.SetName(SidebarToggleButton, label);
    }

    private async Task AnimateSidebarAsync(bool show)
    {
        var version = ++_sidebarAnimationVersion;
        var visual = ElementCompositionPreview.GetElementVisual(PlayerSidebar);
        var compositor = visual.Compositor;
        var easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.16f, 1f),
            new Vector2(0.3f, 1f));

        if (show)
        {
            PlayerSidebar.Visibility = Visibility.Visible;
            visual.Offset = new Vector3(36f, 0f, 0f);
            visual.Opacity = 0f;

            var slideIn = compositor.CreateVector3KeyFrameAnimation();
            slideIn.Duration = TimeSpan.FromMilliseconds(190);
            slideIn.InsertKeyFrame(1f, Vector3.Zero, easing);

            var fadeIn = compositor.CreateScalarKeyFrameAnimation();
            fadeIn.Duration = TimeSpan.FromMilliseconds(150);
            fadeIn.InsertKeyFrame(1f, 1f, easing);

            visual.StartAnimation("Offset", slideIn);
            visual.StartAnimation("Opacity", fadeIn);
            return;
        }

        if (PlayerSidebar.Visibility != Visibility.Visible)
            return;

        var slideOut = compositor.CreateVector3KeyFrameAnimation();
        slideOut.Duration = TimeSpan.FromMilliseconds(160);
        slideOut.InsertKeyFrame(1f, new Vector3(36f, 0f, 0f), easing);

        var fadeOut = compositor.CreateScalarKeyFrameAnimation();
        fadeOut.Duration = TimeSpan.FromMilliseconds(130);
        fadeOut.InsertKeyFrame(1f, 0f, easing);

        visual.StartAnimation("Offset", slideOut);
        visual.StartAnimation("Opacity", fadeOut);

        await Task.Delay(170);

        if (version != _sidebarAnimationVersion)
            return;

        PlayerSidebar.Visibility = Visibility.Collapsed;
        visual.Offset = Vector3.Zero;
        visual.Opacity = 0f;

        if (!_isVideoFullscreen)
            SidebarColumn.Width = new GridLength(0);
    }

    private void ShowFullscreenControls()
    {
        PlayerControlsPanel.Opacity = 1d;
        PlayerControlsPanel.IsHitTestVisible = true;

        if (_isVideoFullscreen && !_fullscreenControlsTimer.IsEnabled)
            _fullscreenControlsTimer.Start();
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
        }
        finally
        {
            _isUpdatingTimeline = false;
        }
    }

    private void ShowStatus(string text)
    {
        PlaybackStatusText.Text = text;
        PlaybackStatusPanel.Visibility = Visibility.Visible;
    }

    private void HideStatus() =>
        PlaybackStatusPanel.Visibility = Visibility.Collapsed;

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        DispatcherQueue.TryEnqueue(() => action());
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
