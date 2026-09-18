using Eizo.Playback;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Eizo.Views;

public sealed partial class PlayerView
{
    private bool _isPictureInPicture;
    private bool _pictureInPictureSidebarWasOpen;
    private Thickness _pictureInPicturePreviousFrameMargin;
    private CornerRadius _pictureInPicturePreviousFrameCornerRadius;
    private GridLength _pictureInPicturePreviousHeaderHeight;
    private GridLength _pictureInPicturePreviousControlsHeight;
    private Thickness _pictureInPicturePreviousControlsPadding;
    private ElementTheme _pictureInPicturePreviousTheme;
    private Visibility _pictureInPicturePreviousCurrentTimeVisibility;
    private Visibility _pictureInPicturePreviousDurationVisibility;
    private double _pictureInPicturePreviousPrimarySubtitleFontSize;
    private double _pictureInPicturePreviousSecondarySubtitleFontSize;
    private Thickness _pictureInPicturePreviousPrimarySubtitlePadding;
    private Thickness _pictureInPicturePreviousSecondarySubtitlePadding;
    private DispatcherTimer? _pictureInPictureControlsTimer;
    private bool _pictureInPicturePointerOverControls;
    private bool _pictureInPictureHandlersAttached;
    private IPlaybackEngine? _pictureInPictureObservedEngine;
    private PointerEventHandler? _pictureInPicturePointerWheelHandler;

    internal bool IsPictureInPicture => _isPictureInPicture;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdatePictureInPictureAccessibility();
    }

    private void PictureInPictureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPreparingForDetach)
            return;

        // Fullscreen and compact-overlay are mutually exclusive presentation modes.
        if (_isVideoFullscreen)
            SetVideoFullscreen(false);

        App.MainWindow?.SetPlayerPictureInPicture(!_isPictureInPicture, this);
    }

    private void ExitPictureInPictureButton_Click(object sender, RoutedEventArgs e) =>
        App.MainWindow?.SetPlayerPictureInPicture(false, this);

    internal void SetPictureInPictureVisualState(bool enabled)
    {
        if (_isPictureInPicture == enabled)
            return;

        if (enabled)
        {
            _pictureInPicturePreviousFrameMargin = PlayerFrame.Margin;
            _pictureInPicturePreviousFrameCornerRadius = PlayerFrame.CornerRadius;
            _pictureInPicturePreviousHeaderHeight = HeaderRow.Height;
            _pictureInPicturePreviousControlsHeight = ControlsRow.Height;
            _pictureInPicturePreviousControlsPadding = PlayerControlsPanel.Padding;
            _pictureInPicturePreviousTheme = PlayerRoot.RequestedTheme;
            _pictureInPicturePreviousCurrentTimeVisibility = CurrentTimeText.Visibility;
            _pictureInPicturePreviousDurationVisibility = DurationText.Visibility;
            _pictureInPicturePreviousPrimarySubtitleFontSize = PrimarySubtitleText.FontSize;
            _pictureInPicturePreviousSecondarySubtitleFontSize = SecondarySubtitleText.FontSize;
            _pictureInPicturePreviousPrimarySubtitlePadding = PrimarySubtitleOverlay.Padding;
            _pictureInPicturePreviousSecondarySubtitlePadding = SecondarySubtitleOverlay.Padding;
            _pictureInPictureSidebarWasOpen = PlayerSplitView.IsPaneOpen;

            _isPictureInPicture = true;
            _pictureInPicturePointerOverControls = false;
            PlayerRoot.RequestedTheme = ElementTheme.Dark;
            PlayerSplitView.IsPaneOpen = false;
            PlayerFrame.Margin = new Thickness(0);
            PlayerFrame.CornerRadius = new CornerRadius(0);
            HeaderRow.Height = new GridLength(0);
            PlayerHeader.Visibility = Visibility.Collapsed;
            PlayerControlsPanel.Padding = new Thickness(10, 6, 10, 8);

            // Keep the compact overlay deliberately sparse. Every remaining action
            // is still an ordinary WinUI Button, so hover/pressed/focus/keyboard
            // states continue to come from the platform template rather than Eizo.
            LeftPlaybackControls.Visibility = Visibility.Collapsed;
            PreviousChapterButton.Visibility = Visibility.Collapsed;
            NextChapterButton.Visibility = Visibility.Collapsed;
            SidebarToggleButton.Visibility = Visibility.Collapsed;
            CurrentTimeText.Visibility = Visibility.Collapsed;
            DurationText.Visibility = Visibility.Collapsed;

            // Smaller subtitle typography keeps two-language subtitles readable in
            // CompactOverlay without changing the user's persisted subtitle offsets.
            PrimarySubtitleText.FontSize = 16d;
            SecondarySubtitleText.FontSize = 14d;
            PrimarySubtitleOverlay.Padding = new Thickness(10, 5, 10, 5);
            SecondarySubtitleOverlay.Padding = new Thickness(10, 5, 10, 5);

            FullscreenButton.Click -= FullscreenButton_Click;
            FullscreenButton.Click += ExitPictureInPictureButton_Click;
            FullscreenIcon.Glyph = "\uE73F"; // Segoe Fluent Icons: BackToWindow

            AttachPictureInPictureHandlers();
            AttachPictureInPictureEngineObserver();
            ApplyPictureInPictureControlLayout();
            UpdatePictureInPictureAccessibility();
            ShowPictureInPictureControls(restartAutoHide: true);
            Focus(FocusState.Programmatic);
            return;
        }

        _isPictureInPicture = false;
        _pictureInPicturePointerOverControls = false;
        StopPictureInPictureAutoHide();
        DetachPictureInPictureEngineObserver();
        DetachPictureInPictureHandlers();

        PlayerRoot.RequestedTheme = _pictureInPicturePreviousTheme;
        PlayerFrame.Margin = _pictureInPicturePreviousFrameMargin;
        PlayerFrame.CornerRadius = _pictureInPicturePreviousFrameCornerRadius;
        HeaderRow.Height = _pictureInPicturePreviousHeaderHeight;
        PlayerHeader.Visibility = Visibility.Visible;
        ControlsRow.Height = _pictureInPicturePreviousControlsHeight;
        PlayerControlsPanel.Padding = _pictureInPicturePreviousControlsPadding;
        PlayerControlsPanel.Opacity = 1d;
        PlayerControlsPanel.IsHitTestVisible = true;

        LeftPlaybackControls.Visibility = Visibility.Visible;
        PreviousChapterButton.Visibility = Visibility.Visible;
        NextChapterButton.Visibility = Visibility.Visible;
        SidebarToggleButton.Visibility = Visibility.Visible;
        CurrentTimeText.Visibility = _pictureInPicturePreviousCurrentTimeVisibility;
        DurationText.Visibility = _pictureInPicturePreviousDurationVisibility;

        PrimarySubtitleText.FontSize = _pictureInPicturePreviousPrimarySubtitleFontSize;
        SecondarySubtitleText.FontSize = _pictureInPicturePreviousSecondarySubtitleFontSize;
        PrimarySubtitleOverlay.Padding = _pictureInPicturePreviousPrimarySubtitlePadding;
        SecondarySubtitleOverlay.Padding = _pictureInPicturePreviousSecondarySubtitlePadding;

        FullscreenButton.Click -= ExitPictureInPictureButton_Click;
        FullscreenButton.Click += FullscreenButton_Click;
        FullscreenIcon.Glyph = "\uE740"; // Segoe Fluent Icons: FullScreen

        ToolTipService.SetToolTip(FullscreenButton, T("Playback_FullScreen"));
        AutomationProperties.SetName(FullscreenButton, T("Playback_FullScreen"));
        UpdatePictureInPictureAccessibility();

        if (_pictureInPictureSidebarWasOpen)
            PlayerSplitView.IsPaneOpen = true;

        // Re-evaluate the normal responsive transport layout after CompactOverlay.
        UpdatePlaybackControlLayout();
        ApplySubtitlePositions();
    }

    private void AttachPictureInPictureHandlers()
    {
        if (_pictureInPictureHandlersAttached)
            return;

        EnsurePictureInPictureControlsTimer();
        PlayerRoot.PointerMoved += PictureInPictureRoot_PointerMoved;
        PlayerRoot.SizeChanged += PictureInPictureRoot_SizeChanged;
        PlayerControlsPanel.PointerEntered += PictureInPictureControls_PointerEntered;
        PlayerControlsPanel.PointerExited += PictureInPictureControls_PointerExited;
        KeyDown += PictureInPicture_KeyDown;
        _pictureInPicturePointerWheelHandler ??= PictureInPictureRoot_PointerWheelChanged;
        PlayerRoot.AddHandler(
            UIElement.PointerWheelChangedEvent,
            _pictureInPicturePointerWheelHandler,
            true);
        Unloaded += PlayerView_PictureInPictureUnloaded;
        _pictureInPictureHandlersAttached = true;
    }

    private void DetachPictureInPictureHandlers()
    {
        if (!_pictureInPictureHandlersAttached)
            return;

        PlayerRoot.PointerMoved -= PictureInPictureRoot_PointerMoved;
        PlayerRoot.SizeChanged -= PictureInPictureRoot_SizeChanged;
        PlayerControlsPanel.PointerEntered -= PictureInPictureControls_PointerEntered;
        PlayerControlsPanel.PointerExited -= PictureInPictureControls_PointerExited;
        KeyDown -= PictureInPicture_KeyDown;
        if (_pictureInPicturePointerWheelHandler is not null)
        {
            PlayerRoot.RemoveHandler(
                UIElement.PointerWheelChangedEvent,
                _pictureInPicturePointerWheelHandler);
        }
        Unloaded -= PlayerView_PictureInPictureUnloaded;
        _pictureInPictureHandlersAttached = false;
    }

    private void AttachPictureInPictureEngineObserver()
    {
        DetachPictureInPictureEngineObserver();
        _pictureInPictureObservedEngine = _engine;
        if (_pictureInPictureObservedEngine is not null)
            _pictureInPictureObservedEngine.StateChanged += PictureInPictureEngine_StateChanged;
    }

    private void DetachPictureInPictureEngineObserver()
    {
        if (_pictureInPictureObservedEngine is not null)
            _pictureInPictureObservedEngine.StateChanged -= PictureInPictureEngine_StateChanged;
        _pictureInPictureObservedEngine = null;
    }

    private void EnsurePictureInPictureControlsTimer()
    {
        if (_pictureInPictureControlsTimer is not null)
            return;

        _pictureInPictureControlsTimer = new DispatcherTimer();
        _pictureInPictureControlsTimer.Tick += PictureInPictureControlsTimer_Tick;
    }

    private void PictureInPictureRoot_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isPictureInPicture)
            ShowPictureInPictureControls(restartAutoHide: true);
    }

    private void PictureInPictureRoot_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPictureInPicture ||
            _engine is null ||
            _currentSource is null)
        {
            return;
        }

        var delta = e.GetCurrentPoint(PlayerRoot).Properties.MouseWheelDelta;
        if (delta == 0)
            return;

        SetVolume(_volume + (Math.Sign(delta) * 0.05d));
        ShowPictureInPictureControls(restartAutoHide: true);
        e.Handled = true;
    }

    private void PictureInPictureRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isPictureInPicture)
            return;

        // PlayerView's normal responsive pass also runs for this SizeChanged event.
        // Re-apply the dedicated CompactOverlay geometry afterwards so the regular
        // <760px two-row transport layout never expands the PiP controls to 150px.
        PlayerSplitView.IsPaneOpen = false;
        ApplyPictureInPictureControlLayout();
    }

    private void PictureInPictureControls_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPictureInPicture)
            return;

        _pictureInPicturePointerOverControls = true;
        ShowPictureInPictureControls(restartAutoHide: false);
    }

    private void PictureInPictureControls_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPictureInPicture)
            return;

        _pictureInPicturePointerOverControls = false;
        RestartPictureInPictureAutoHide();
    }

    private void PictureInPicture_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isPictureInPicture || e.Key != VirtualKey.Escape)
            return;

        App.MainWindow?.SetPlayerPictureInPicture(false, this);
        e.Handled = true;
    }

    private void PictureInPictureEngine_StateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isPictureInPicture || !ReferenceEquals(sender, _pictureInPictureObservedEngine))
                return;

            if (e.CurrentState == PlaybackState.Playing)
                ShowPictureInPictureControls(restartAutoHide: true);
            else
                ShowPictureInPictureControls(restartAutoHide: false);
        });
    }

    private void PictureInPictureControlsTimer_Tick(object? sender, object e)
    {
        StopPictureInPictureAutoHide();

        if (!_isPictureInPicture ||
            _pictureInPicturePointerOverControls ||
            _engine?.State != PlaybackState.Playing ||
            _isScrubbingTimeline)
        {
            return;
        }

        PlayerControlsPanel.Opacity = 0d;
        PlayerControlsPanel.IsHitTestVisible = false;
    }

    private void ShowPictureInPictureControls(bool restartAutoHide)
    {
        if (!_isPictureInPicture)
            return;

        PlayerControlsPanel.Opacity = 1d;
        PlayerControlsPanel.IsHitTestVisible = true;

        if (restartAutoHide)
            RestartPictureInPictureAutoHide();
        else
            StopPictureInPictureAutoHide();
    }

    private void RestartPictureInPictureAutoHide()
    {
        EnsurePictureInPictureControlsTimer();
        StopPictureInPictureAutoHide();

        if (!_isPictureInPicture ||
            _pictureInPicturePointerOverControls ||
            _engine?.State != PlaybackState.Playing ||
            _isScrubbingTimeline)
        {
            return;
        }

        var timeoutSeconds = AppSettingsStore.NormalizeFullscreenControlsTimeout(
            AppSettingsStore.Current.FullscreenControlsTimeoutSeconds);
        _pictureInPictureControlsTimer!.Interval = TimeSpan.FromSeconds(timeoutSeconds);
        _pictureInPictureControlsTimer.Start();
    }

    private void StopPictureInPictureAutoHide() =>
        _pictureInPictureControlsTimer?.Stop();

    private void ApplyPictureInPictureControlLayout()
    {
        ControlsRow.Height = new GridLength(88d);

        LeftPlaybackColumn.Width = new GridLength(1d, GridUnitType.Star);
        CenterPlaybackColumn.Width = GridLength.Auto;
        RightPlaybackColumn.Width = new GridLength(1d, GridUnitType.Star);

        Grid.SetRow(CenterPlaybackControls, 0);
        Grid.SetColumn(CenterPlaybackControls, 1);
        Grid.SetColumnSpan(CenterPlaybackControls, 1);
        CenterPlaybackControls.Margin = new Thickness(0);
    }

    private void UpdatePictureInPictureAccessibility()
    {
        if (PictureInPictureButton is not null)
        {
            var enterLabel = GetPictureInPictureLabel(exit: false);
            ToolTipService.SetToolTip(PictureInPictureButton, enterLabel);
            AutomationProperties.SetName(PictureInPictureButton, enterLabel);
        }

        if (_isPictureInPicture && FullscreenButton is not null)
        {
            var exitLabel = GetPictureInPictureLabel(exit: true);
            ToolTipService.SetToolTip(FullscreenButton, exitLabel);
            AutomationProperties.SetName(FullscreenButton, exitLabel);
        }
    }

    private string GetPictureInPictureLabel(bool exit) =>
        T(exit
            ? "Playback_ExitPictureInPicture"
            : "Playback_PictureInPicture");

    private void PlayerView_PictureInPictureUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_isPictureInPicture)
            return;

        App.MainWindow?.SetPlayerPictureInPicture(false, this);
    }
}
