using Eizo.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Eizo;

public sealed partial class MainWindow
{
    private PlayerView? _pictureInPictureOwner;
    private bool _playerPictureInPicture;
    private bool _restoreMaximizedAfterPictureInPicture;

    public void SetPlayerPictureInPicture(bool enabled, PlayerView? owner = null)
    {
        if (enabled && owner is null)
            return;

        if (!enabled &&
            owner is not null &&
            !ReferenceEquals(_pictureInPictureOwner, owner))
        {
            return;
        }

        if (_playerPictureInPicture == enabled)
            return;

        if (enabled)
        {
            // The same PlaybackView remains loaded in the same XAML tree. This is
            // intentional: CompactOverlay changes only the native window presenter,
            // so LibVLC keeps its current engine/swap-chain without reopening media.
            if (_playerFullscreen && _fullscreenOwner is not null)
                _fullscreenOwner.ExitFullscreenFromKeyboard();

            _restoreMaximizedAfterPictureInPicture =
                AppWindow.Presenter is OverlappedPresenter
                {
                    State: OverlappedPresenterState.Maximized
                };

            _pictureInPictureOwner = owner;
            _playerPictureInPicture = true;
            owner!.SetPictureInPictureVisualState(true);

            AppTitleBar.Visibility = Visibility.Collapsed;
            RootGrid.RowDefinitions[0].Height = new GridLength(0);
            Grid.SetRow(ShellNavigation, 0);
            Grid.SetRowSpan(ShellNavigation, 2);
            ShowImmersiveChrome();

            if (AppWindow.Presenter is not { Kind: AppWindowPresenterKind.CompactOverlay })
            {
                var presenter = CompactOverlayPresenter.Create();
                presenter.InitialSize = CompactOverlaySize.Medium;
                AppWindow.SetPresenter(presenter);
            }

            return;
        }

        var previousOwner = _pictureInPictureOwner;
        _pictureInPictureOwner = null;
        _playerPictureInPicture = false;
        previousOwner?.SetPictureInPictureVisualState(false);

        if (AppWindow.Presenter is not { Kind: AppWindowPresenterKind.Overlapped })
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        Grid.SetRow(ShellNavigation, 1);
        Grid.SetRowSpan(ShellNavigation, 1);
        RootGrid.RowDefinitions[0].Height = new GridLength(48);
        AppTitleBar.Visibility = Visibility.Visible;
        ShowNavigationChrome();

        if (_restoreMaximizedAfterPictureInPicture)
        {
            _restoreMaximizedAfterPictureInPicture = false;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (AppWindow.Presenter is OverlappedPresenter restoredPresenter)
                    restoredPresenter.Maximize();
            });
        }
    }
}
