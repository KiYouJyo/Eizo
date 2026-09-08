using Eizo.Localization;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class SettingsView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;

    public SettingsView()
    {
        InitializeComponent();
        ApplyText();
    }

    private string T(string key) => _localization.GetString(key);

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
