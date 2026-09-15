using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Eizo;

internal sealed partial class StartupWindow : Window
{
    private readonly TaskCompletionSource<bool> _firstFramePresented =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _renderHooked;

    public StartupWindow()
    {
        InitializeComponent();

        Title = "Eizo 映藏";
        StartupRoot.RequestedTheme = ThemePreferenceStore.Load();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Eizo.ico"));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(StartupTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        RestoreWindowPlacement();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            // Keep the lightweight startup surface above the main window while the
            // heavy shell is constructed and paints its first real splash frame.
            presenter.IsAlwaysOnTop = true;
        }

        Closed += (_, _) => _firstFramePresented.TrySetResult(true);
    }

    internal Task WaitForFirstFrameAsync() => _firstFramePresented.Task;

    private void StartupRoot_Loaded(object sender, RoutedEventArgs e)
    {
        if (_renderHooked) return;
        _renderHooked = true;
        CompositionTarget.Rendering += OnFirstFrameRendering;
    }

    private void OnFirstFrameRendering(object? sender, object e)
    {
        CompositionTarget.Rendering -= OnFirstFrameRendering;
        _firstFramePresented.TrySetResult(true);
    }

    private void RestoreWindowPlacement()
    {
        var workArea = DisplayArea
            .GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary)
            .WorkArea;

        var placement = new WindowPlacementService().Load(
            new SizeInt32(workArea.Width, workArea.Height));

        AppWindow.Resize(new SizeInt32(placement.Width, placement.Height));

        if (placement.WasMaximized &&
            AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }
}
