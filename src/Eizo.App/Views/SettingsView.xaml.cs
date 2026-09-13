using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class SettingsView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly BangumiAccountService _bangumiAccount = BangumiAccountService.Default;
    private bool _isSynchronizing;

    public SettingsView()
    {
        InitializeComponent();
        ApplyText();
        Loaded += SettingsView_Loaded;
        Unloaded += SettingsView_Unloaded;
        _bangumiAccount.Changed += BangumiAccount_Changed;
    }

    private string T(string key) => _localization.GetString(key);

    private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        _isSynchronizing = true;
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

            LanguagePicker.SelectedIndex = AppSettingsStore.Current.Language switch
            {
                AppLanguagePreference.SimplifiedChinese => 1,
                AppLanguagePreference.Japanese => 2,
                AppLanguagePreference.English => 3,
                _ => 0
            };
        }
        finally
        {
            _isSynchronizing = false;
        }

        await RefreshBangumiAccountStateAsync(
            forceRefresh: false);
    }

    private void SettingsView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _bangumiAccount.Changed -= BangumiAccount_Changed;
    }

    private async void BangumiAccount_Changed(
        object? sender,
        EventArgs e)
    {
        await RefreshBangumiAccountStateAsync(
            forceRefresh: false);
    }

    private async Task RefreshBangumiAccountStateAsync(
        bool forceRefresh)
    {
        if (!_bangumiAccount.IsConnected)
        {
            BangumiAccountStatusText.Text =
                T("Bangumi_AccountNotConnected");
            BangumiConnectButton.Visibility =
                Visibility.Visible;
            BangumiDisconnectButton.Visibility =
                Visibility.Collapsed;
            return;
        }

        BangumiAccountStatusText.Text =
            T("Bangumi_AccountChecking");

        try
        {
            var profile =
                await _bangumiAccount.GetProfileAsync(
                    forceRefresh);

            if (profile is null)
            {
                BangumiAccountStatusText.Text =
                    T("Bangumi_AccountNotConnected");
                BangumiConnectButton.Visibility =
                    Visibility.Visible;
                BangumiDisconnectButton.Visibility =
                    Visibility.Collapsed;
                return;
            }

            var displayName =
                string.IsNullOrWhiteSpace(profile.NickName)
                    ? profile.UserName
                    : profile.NickName;

            BangumiAccountStatusText.Text =
                string.Format(
                    T("Bangumi_AccountConnectedFormat"),
                    displayName,
                    profile.UserName);
            BangumiConnectButton.Visibility =
                Visibility.Collapsed;
            BangumiDisconnectButton.Visibility =
                Visibility.Visible;
        }
        catch
        {
            BangumiAccountStatusText.Text =
                T("Bangumi_AccountNetworkError");
            BangumiConnectButton.Visibility =
                Visibility.Collapsed;
            BangumiDisconnectButton.Visibility =
                Visibility.Visible;
        }
    }

    private async void BangumiConnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (XamlRoot is null)
            return;

        await BangumiAccountDialogService.ShowConnectAsync(
            XamlRoot);
    }

    private async void BangumiDisconnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (XamlRoot is null)
            return;

        await BangumiAccountDialogService.ConfirmDisconnectAsync(
            XamlRoot);
    }

    private void AppearanceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing ||
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

    private async void LanguagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing || LanguagePicker.SelectedIndex < 0) return;

        var preference = LanguagePicker.SelectedIndex switch
        {
            1 => AppLanguagePreference.SimplifiedChinese,
            2 => AppLanguagePreference.Japanese,
            3 => AppLanguagePreference.English,
            _ => AppLanguagePreference.System
        };

        LanguagePicker.IsEnabled = false;
        var switched = await _localization.SwitchLanguageAsync(preference);
        LanguagePicker.IsEnabled = true;
        if (switched) return;

        _isSynchronizing = true;
        try
        {
            LanguagePicker.SelectedIndex = AppSettingsStore.Current.Language switch
            {
                AppLanguagePreference.SimplifiedChinese => 1,
                AppLanguagePreference.Japanese => 2,
                AppLanguagePreference.English => 3,
                _ => 0
            };
        }
        finally
        {
            _isSynchronizing = false;
        }
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
        LanguagePicker.ItemsSource = new[]
        {
            T("Settings_LanguageSystem"),
            "简体中文",
            "日本語",
            "English"
        };

        AppearanceTitle.Text = T("Settings_Appearance");
        AppearanceDescription.Text = T("Settings_AppearanceDescription");
        AppearanceCombo.ItemsSource = new[]
        {
            T("Settings_AppearanceSystem"),
            T("Settings_AppearanceLight"),
            T("Settings_AppearanceDark")
        };

        BangumiSectionTitle.Text = T("Bangumi_SettingsSection");
        BangumiAccountTitle.Text = T("Bangumi_Account");
        BangumiAccountDescription.Text = T("Bangumi_AccountSettingsDescription");
        BangumiTokenPageButton.Content = T("Bangumi_OpenTokenPage");
        BangumiConnectButton.Content = T("Bangumi_Connect");
        BangumiDisconnectButton.Content = T("Bangumi_Disconnect");

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
