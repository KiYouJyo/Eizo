using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

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

    internal bool IsPictureInPicture => _isPictureInPicture;

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
            _pictureInPictureSidebarWasOpen = PlayerSplitView.IsPaneOpen;

            _isPictureInPicture = true;
            PlayerSplitView.IsPaneOpen = false;
            PlayerFrame.Margin = new Thickness(0);
            PlayerFrame.CornerRadius = new CornerRadius(0);
            HeaderRow.Height = new GridLength(0);
            PlayerHeader.Visibility = Visibility.Collapsed;
            ControlsRow.Height = new GridLength(76);
            PlayerControlsPanel.Padding = new Thickness(10, 6, 10, 6);

            // PiP deliberately keeps only the high-frequency transport actions.
            // These are ordinary WinUI Buttons, so pointer-over, pressed, focus,
            // keyboard and accessibility behavior all come from the native template.
            LeftPlaybackControls.Visibility = Visibility.Collapsed;
            PreviousChapterButton.Visibility = Visibility.Collapsed;
            NextChapterButton.Visibility = Visibility.Collapsed;
            SidebarToggleButton.Visibility = Visibility.Collapsed;

            FullscreenButton.Click -= FullscreenButton_Click;
            FullscreenButton.Click += ExitPictureInPictureButton_Click;
            FullscreenIcon.Glyph = "\uE73F"; // Segoe Fluent Icons: BackToWindow

            const string exitLabel = "Exit picture in picture";
            ToolTipService.SetToolTip(FullscreenButton, exitLabel);
            AutomationProperties.SetName(FullscreenButton, exitLabel);

            Unloaded += PlayerView_PictureInPictureUnloaded;
            return;
        }

        _isPictureInPicture = false;
        Unloaded -= PlayerView_PictureInPictureUnloaded;

        PlayerFrame.Margin = _pictureInPicturePreviousFrameMargin;
        PlayerFrame.CornerRadius = _pictureInPicturePreviousFrameCornerRadius;
        HeaderRow.Height = _pictureInPicturePreviousHeaderHeight;
        PlayerHeader.Visibility = Visibility.Visible;
        ControlsRow.Height = _pictureInPicturePreviousControlsHeight;
        PlayerControlsPanel.Padding = _pictureInPicturePreviousControlsPadding;

        LeftPlaybackControls.Visibility = Visibility.Visible;
        PreviousChapterButton.Visibility = Visibility.Visible;
        NextChapterButton.Visibility = Visibility.Visible;
        SidebarToggleButton.Visibility = Visibility.Visible;

        FullscreenButton.Click -= ExitPictureInPictureButton_Click;
        FullscreenButton.Click += FullscreenButton_Click;
        FullscreenIcon.Glyph = "\uE740"; // Segoe Fluent Icons: FullScreen

        ToolTipService.SetToolTip(FullscreenButton, T("Playback_FullScreen"));
        AutomationProperties.SetName(FullscreenButton, T("Playback_FullScreen"));

        if (_pictureInPictureSidebarWasOpen)
            PlayerSplitView.IsPaneOpen = true;
    }

    private void PlayerView_PictureInPictureUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_isPictureInPicture)
            return;

        App.MainWindow?.SetPlayerPictureInPicture(false, this);
    }
}
