using Eizo.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class SettingsView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private bool _isSynchronizingTheme;

    public SettingsView()
    {
        InitializeComponent();
        ApplyText();
        Loaded += SettingsView_Loaded;
    }

    private string T(string key) => _localization.GetString(key);

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        _isSynchronizingTheme = true;
        try
        {
            var requestedTheme =
                (XamlRoot?.Content as FrameworkElement)?.RequestedTheme
                ?? ThemePreferenceStore.Load();

            AppearanceCombo.SelectedIndex = requestedTheme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };
        }
        finally
        {
            _isSynchronizingTheme = false;
        }
    }

    private void AppearanceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingTheme ||
            AppearanceCombo.SelectedIndex < 0 ||
            XamlRoot?.Content is not FrameworkElement root)
        {
            return;
        }

        var theme = AppearanceCombo.SelectedIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        root.RequestedTheme = theme;
        ThemePreferenceStore.Save(theme);
    }

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Settings");
        PageSubtitle.Text = T("Settings_Subtitle");

        SettingsSectionList.ItemsSource = new[]
        {
            T("Settings_General"),
            T("Settings_Metadata"),
            T("Settings_Playback"),
            T("Settings_SubtitlesAudio"),
            T("Settings_Network"),
            T("Settings_Privacy")
        };

        LanguageSectionTitle.Text = T("Settings_LanguageAppearance");
        LanguageTitle.Text = T("Settings_Language");
        LanguageDescription.Text = T("Settings_LanguageDescription");

        AppearanceTitle.Text = T("Settings_Appearance");
        AppearanceDescription.Text = T("Settings_AppearanceDescription");
        AppearanceCombo.ItemsSource = new[]
        {
            T("Settings_AppearanceSystem"),
            T("Settings_AppearanceLight"),
            T("Settings_AppearanceDark")
        };

        JapaneseMediaTitle.Text = T("Settings_JapaneseMedia");
        PreferredTitleLabel.Text = T("Settings_PreferredTitle");
        PreferredTitleDescription.Text = T("Settings_PreferredTitleDescription");
        PreferredTitleCombo.ItemsSource = new[]
        {
            "简体中文",
            "日本語",
            "English"
        };

        JapaneseTitleLabel.Text = T("Settings_ShowJapaneseTitle");
        JapaneseTitleDescription.Text = T("Settings_ShowJapaneseTitleDescription");
        RomajiLabel.Text = T("Settings_ShowRomaji");
        RomajiDescription.Text = T("Settings_ShowRomajiDescription");
    }
}
