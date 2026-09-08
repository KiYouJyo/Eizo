using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace Eizo.Controls;

/// <summary>
/// Reuses the SpatialViewer/UrbanPlanToolbox native ComboBox popup surface
/// resources without replacing WinUI templates or visual states.
/// </summary>
public static class TransientComboBoxTheme
{
    public static readonly DependencyProperty ApplyProperty = DependencyProperty.RegisterAttached(
        "Apply",
        typeof(bool),
        typeof(TransientComboBoxTheme),
        new PropertyMetadata(false, OnApplyChanged));

    public static bool GetApply(DependencyObject element) => (bool)element.GetValue(ApplyProperty);

    public static void SetApply(DependencyObject element, bool value) =>
        element.SetValue(ApplyProperty, value);

    private static void OnApplyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ComboBox comboBox || args.NewValue is not true) return;
        comboBox.Loaded += OnComboBoxLoaded;
        comboBox.ActualThemeChanged += OnComboBoxActualThemeChanged;
    }

    private static void OnComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox) SyncPopupResources(comboBox);
    }

    private static void OnComboBoxActualThemeChanged(FrameworkElement sender, object e)
    {
        if (sender is ComboBox comboBox) SyncPopupResources(comboBox);
    }

    private static void SyncPopupResources(ComboBox comboBox)
    {
        var themeKey = new AccessibilitySettings().HighContrast
            ? "HighContrast"
            : comboBox.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";

        if (Application.Current.Resources.ThemeDictionaries[themeKey] is not ResourceDictionary resources)
            return;

        comboBox.Resources["ComboBoxDropDownBackground"] =
            resources["AppTransientSurfaceBrush"] as Brush;
        comboBox.Resources["ComboBoxDropDownBorderBrush"] =
            resources["AppTransientSurfaceBorderBrush"] as Brush;
    }
}
