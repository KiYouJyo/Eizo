using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace Eizo;

public sealed partial class MainWindow
{
    internal const double PreferredTabWidth = 220;
    private const double MinimumTabWidth = 72;
    private const double ShellTabSpacing = 8;
    private const double NewTabButtonWidth = 32;
    private static readonly Duration TabOpenDuration = new(TimeSpan.FromMilliseconds(190));

    private bool _adaptiveTabSizingInitialized;
    private SplitView? _navigationSplitView;
    private bool _navigationPaneBackgroundHooked;
    private bool _isWindowActive = true;
    private bool _shellReady;

    private void ShellNavigation_Loaded(object sender, RoutedEventArgs e)
    {
        HookNavigationPaneBackground();
        _shellReady = true;
        ApplyResponsiveLayout(force: true);
        DispatcherQueue.TryEnqueue(() => ApplyResponsiveLayout(force: true));
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_shellReady) ApplyResponsiveLayout();
    }

    private void ConfigureTabInteractions(Border container, double preferredWidth)
    {
        EnsureAdaptiveTabSizing();
        container.Unloaded += ShellTab_Unloaded;

        var adaptiveTargetWidth = ApplyAdaptiveTabWidths(container, preferredWidth);
        AnimateTabOpen(container, adaptiveTargetWidth);
    }

    private void EnsureAdaptiveTabSizing()
    {
        if (_adaptiveTabSizingInitialized) return;
        _adaptiveTabSizingInitialized = true;
        AppTitleBar.SizeChanged += AppTitleBar_AdaptiveTabsSizeChanged;
    }

    private void AppTitleBar_AdaptiveTabsSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyAdaptiveTabWidths();

    private void ShellTab_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
            element.Unloaded -= ShellTab_Unloaded;

        DispatcherQueue.TryEnqueue(() => ApplyAdaptiveTabWidths());
    }

    private double ApplyAdaptiveTabWidths(Border? openingTab = null, double preferredWidth = PreferredTabWidth)
    {
        var tabCount = ShellTabItems.Children.Count;
        if (tabCount <= 0) return preferredWidth;

        var titleBarWidth = AppTitleBar.ActualWidth;
        if (!double.IsFinite(titleBarWidth) || titleBarWidth <= 0) return preferredWidth;

        // Same algorithm as SpatialViewer. Eizo uses a 128-DIP product column
        // instead of SpatialViewer's 104-DIP product column.
        const double fixedTitleBarWidth = 128 + 132 + 12 + 12;
        var tabViewportWidth = Math.Max(0, titleBarWidth - fixedTitleBarWidth);
        var spacingWidth = ShellTabSpacing * tabCount;
        var usableTabWidth = Math.Max(0, tabViewportWidth - NewTabButtonWidth - spacingWidth);
        var calculatedWidth = usableTabWidth / tabCount;
        var targetWidth = Math.Clamp(
            calculatedWidth,
            MinimumTabWidth,
            Math.Min(PreferredTabWidth, preferredWidth));

        foreach (var child in ShellTabItems.Children)
        {
            if (child is not Border tab || ReferenceEquals(tab, openingTab)) continue;
            tab.Width = targetWidth;
        }

        return targetWidth;
    }

    private void AnimateTabOpen(Border container, double targetWidth)
    {
        if (!RootGrid.IsLoaded)
        {
            container.Width = targetWidth;
            container.Opacity = 1;
            return;
        }

        container.Width = 0;
        container.Opacity = 0;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var widthAnimation = new DoubleAnimation
        {
            From = 0,
            To = targetWidth,
            Duration = TabOpenDuration,
            EasingFunction = easing,
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(widthAnimation, container);
        Storyboard.SetTargetProperty(widthAnimation, nameof(FrameworkElement.Width));

        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(135)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(opacityAnimation, container);
        Storyboard.SetTargetProperty(opacityAnimation, nameof(UIElement.Opacity));

        var storyboard = new Storyboard();
        storyboard.Children.Add(widthAnimation);
        storyboard.Children.Add(opacityAnimation);
        storyboard.Completed += (_, _) =>
        {
            if (container.Parent is null) return;
            container.Opacity = 1;
            ApplyAdaptiveTabWidths();
        };
        storyboard.Begin();
    }

    private void RefreshTabVisuals()
    {
        var dark = WindowRoot.ActualTheme == ElementTheme.Dark;
        foreach (var state in _tabs.Values)
        {
            if (state.Visual is null) continue;
            ApplyTabVisual(
                state.Visual,
                string.Equals(state.Key, _selectedTabKey, StringComparison.Ordinal),
                dark);
        }
    }

    private static void ApplyTabVisual(ShellTabVisual visual, bool selected, bool dark)
    {
        visual.Container.Background = new SolidColorBrush(selected
            ? (dark
                ? ColorHelper.FromArgb(30, 255, 255, 255)
                : ColorHelper.FromArgb(214, 255, 255, 255))
            : (dark
                ? ColorHelper.FromArgb(10, 255, 255, 255)
                : ColorHelper.FromArgb(8, 0, 0, 0)));

        visual.Container.BorderThickness = new Thickness(selected ? 1 : 0);
        visual.Container.BorderBrush = new SolidColorBrush(
            dark
                ? ColorHelper.FromArgb(38, 255, 255, 255)
                : ColorHelper.FromArgb(51, 117, 117, 117));

        visual.HeaderText.FontWeight = selected
            ? Microsoft.UI.Text.FontWeights.SemiBold
            : Microsoft.UI.Text.FontWeights.Normal;
        visual.HeaderText.Opacity = 1;
    }

    private void UpdateTitleBarColors()
    {
        var dark = WindowRoot.ActualTheme == ElementTheme.Dark;

        if (AppWindowTitleBar.IsCustomizationSupported())
            AppWindow.TitleBar.PreferredTheme = dark ? TitleBarTheme.Dark : TitleBarTheme.Light;

        var foreground = dark
            ? Color.FromArgb(255, 240, 245, 245)
            : Color.FromArgb(255, 21, 32, 32);
        var hoverBackground = dark
            ? Color.FromArgb(32, 255, 255, 255)
            : Color.FromArgb(24, 0, 0, 0);
        var pressedBackground = dark
            ? Color.FromArgb(48, 255, 255, 255)
            : Color.FromArgb(40, 0, 0, 0);

        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverForegroundColor = foreground;
        AppWindow.TitleBar.ButtonPressedForegroundColor = foreground;
        AppWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonHoverBackgroundColor = hoverBackground;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = pressedBackground;
    }

    private void HookNavigationPaneBackground()
    {
        if (!_navigationPaneBackgroundHooked)
        {
            _navigationPaneBackgroundHooked = true;
            // In CompactOverlay, NavigationView deliberately switches to
            // PaneNotOverlaying after the pane closes and restores its native
            // NavigationViewExpandedPaneBackground (transparent). Do not write
            // PaneBackground from PaneClosed: the compact rail should fall back
            // to the window Mica, exactly like UrbanPlanToolbox.
            ShellNavigation.PaneOpening += (_, _) => QueueNavigationPaneBackgroundUpdate();
            ShellNavigation.ActualThemeChanged += (_, _) => QueueNavigationPaneBackgroundUpdate();
            Activated += MainWindow_ActivatedForNavigationPane;
        }

        QueueNavigationPaneBackgroundUpdate();
    }

    private void MainWindow_ActivatedForNavigationPane(
        object sender,
        WindowActivatedEventArgs args)
    {
        _isWindowActive =
            args.WindowActivationState != WindowActivationState.Deactivated;
        QueueNavigationPaneBackgroundUpdate();
    }

    private void QueueNavigationPaneBackgroundUpdate() =>
        DispatcherQueue.TryEnqueue(() => ApplySharedNavigationPaneBackground());

    private void ApplySharedNavigationPaneBackground()
    {
        // Closed CompactOverlay is owned by NavigationView's PaneNotOverlaying
        // state. Leaving that state untouched keeps the compact rail on the
        // native transparent/Mica surface and prevents stale custom brushes
        // from being reintroduced after an expand/collapse cycle.
        if (!ShellNavigation.IsPaneOpen) return;

        _navigationSplitView ??= FindDescendant<SplitView>(ShellNavigation);

        var themeKey = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast
            ? "HighContrast"
            : ShellNavigation.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";

        var brushKey = _isWindowActive
            ? "ShellNavigationPaneBackgroundBrush"
            : "ShellNavigationPaneInactiveBackgroundBrush";

        var themeResources =
            Application.Current.Resources.ThemeDictionaries[themeKey]
            as ResourceDictionary;

        if (_navigationSplitView is not null &&
            themeResources?[brushKey] is Brush brush)
        {
            _navigationSplitView.PaneBackground = brush;
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;

            var descendant = FindDescendant<T>(child);
            if (descendant is not null) return descendant;
        }

        return null;
    }
}
