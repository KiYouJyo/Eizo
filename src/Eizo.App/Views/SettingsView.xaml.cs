using Eizo.Localization; using Microsoft.UI.Xaml.Controls;
namespace Eizo.Views;
public sealed partial class SettingsView:UserControl
{
 private readonly AppLocalizationService _l=AppLocalizationService.Default;
 public SettingsView(){InitializeComponent();ApplyText();}
 private string T(string k)=>_l.GetString(k);
 private void ApplyText(){PageTitle.Text=T("Nav_Settings");PageSubtitle.Text=T("Settings_Subtitle");GeneralTab.Content=T("Settings_General");MetadataTab.Content=T("Settings_Metadata");PlaybackTab.Content=T("Settings_Playback");SubtitleTab.Content=T("Settings_SubtitlesAudio");NetworkTab.Content=T("Settings_Network");PrivacyTab.Content=T("Settings_Privacy");LanguageSectionTitle.Text=T("Settings_LanguageAppearance");LanguageTitle.Text=T("Settings_Language");LanguageDescription.Text=T("Settings_LanguageDescription");AppearanceTitle.Text=T("Settings_Appearance");AppearanceDescription.Text=T("Settings_AppearanceDescription");AppearanceCombo.ItemsSource=new[]{T("Settings_AppearanceSystem"),T("Settings_AppearanceLight"),T("Settings_AppearanceDark")};JapaneseMediaTitle.Text=T("Settings_JapaneseMedia");PreferredTitleLabel.Text=T("Settings_PreferredTitle");PreferredTitleDescription.Text=T("Settings_PreferredTitleDescription");PreferredTitleCombo.ItemsSource=new[]{T("Settings_TitleChineseJapanese"),T("Settings_TitleJapanese"),T("Settings_TitleEnglish")};JapaneseTitleLabel.Text=T("Settings_ShowJapaneseTitle");JapaneseTitleDescription.Text=T("Settings_ShowJapaneseTitleDescription");RomajiLabel.Text=T("Settings_ShowRomaji");RomajiDescription.Text=T("Settings_ShowRomajiDescription");}
}
