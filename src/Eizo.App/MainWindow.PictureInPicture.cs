using Eizo.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Eizo;

public sealed partial class MainWindow
{
    private PlayerView? _pictureInPictureOwner;
    private bool _playerPictureInPicture;
    private bool _restoreMaximizedAfterPictureInPicture;
    private bool _pictureInPictureWasNavigationChromeHidden;
    private PointInt32 _pictureInPictureRestorePosition;
    private SizeInt32 _pictureInPictureRestoreSize;

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
            if (_playerFullscreen)
                return;

            _restoreMaximizedAfterPictureInPicture =
                AppWindow.Presenter is OverlappedPresenter
                {
                    State: OverlappedPresenterState.Maximized
                };
            _pictureInPictureRestorePosition = AppWindow.Position;
            _pictureInPictureRestoreSize = AppWindow.Size;
            _pictureInPictureWasNavigationChromeHidden =
                _navigationChromeHiddenForImmersive;

            _pictureInPictureOwner = owner;
            _playerPictureInPicture = true;
            owner!.SetPictureInPictureVisualState(true);

            AppTitleBar.Visibility = Visibility.Collapsed;
            RootGrid.RowDefinitions[0].Height = new GridLength(0);
            Grid.SetRow(ShellNavigation, 0);
            Grid.SetRowSpan(ShellNavigation, 2);
            ShowImmersiveChrome();

            AppWindow.Changed += PictureInPicture_AppWindowChanged;

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
        AppWindow.Changed -= PictureInPicture_AppWindowChanged;
        previousOwner?.SetPictureInPictureVisualState(false);

        if (AppWindow.Presenter is not { Kind: AppWindowPresenterKind.Overlapped })
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        Grid.SetRow(ShellNavigation, 1);
        Grid.SetRowSpan(ShellNavigation, 1);
        RootGrid.RowDefinitions[0].Height = new GridLength(48);
        AppTitleBar.Visibility = Visibility.Visible;

        if (_pictureInPictureWasNavigationChromeHidden)
            ShowImmersiveChrome();
        else
            ShowNavigationChrome();

        var restoreMaximized = _restoreMaximizedAfterPictureInPicture;
        _restoreMaximizedAfterPictureInPicture = false;
        var restorePosition = _pictureInPictureRestorePosition;
        var restoreSize = _pictureInPictureRestoreSize;

        // Presenter transitions are re-entrant. Restore geometry on the next UI
        // pass after Windows has finished replacing CompactOverlay with Overlapped.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (restoreMaximized)
            {
                if (AppWindow.Presenter is OverlappedPresenter restoredPresenter)
                    restoredPresenter.Maximize();
                return;
            }

            if (restoreSize.Width > 0 && restoreSize.Height > 0)
            {
                AppWindow.MoveAndResize(
                    new RectInt32(
                        restorePosition.X,
                        restorePosition.Y,
                        restoreSize.Width,
                        restoreSize.Height));
            }
        });
    }

    private void PictureInPicture_AppWindowChanged(
        AppWindow sender,
        AppWindowChangedEventArgs args)
    {
        if (!_playerPictureInPicture ||
            sender.Presenter.Kind == AppWindowPresenterKind.CompactOverlay)
        {
            return;
        }

        // Keep the XAML compact state synchronized even if Windows or another
        // presenter transition leaves CompactOverlay outside the PiP button path.
        var owner = _pictureInPictureOwner;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_playerPictureInPicture &&
                AppWindow.Presenter.Kind != AppWindowPresenterKind.CompactOverlay)
            {
                SetPlayerPictureInPicture(false, owner);
            }
        });
    }
}
