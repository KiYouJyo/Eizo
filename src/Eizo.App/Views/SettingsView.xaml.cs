using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class SettingsView : UserControl
{
    private static readonly double[] PlaybackRates =
        [0.5d, 0.75d, 1d, 1.25d, 1.5d, 2d];

    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiAccountService _bangumiAccount =
        BangumiAccountService.Default;
    private bool _isSynchronizing;
    private bool _metadataMaintenanceRunning;

    public SettingsView()
    {
        InitializeComponent();
        ApplyText();
        Loaded += SettingsView_Loaded;
        Unloaded += SettingsView_Unloaded;
    }

    private string T(string key) => _localization.GetString(key);

    private async void SettingsView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _bangumiAccount.Changed -= BangumiAccount_Changed;
        _bangumiAccount.Changed += BangumiAccount_Changed;
        AppSettingsStore.Changed -= AppSettingsStore_Changed;
        AppSettingsStore.Changed += AppSettingsStore_Changed;

        SyncControlsFromSettings();
        await RefreshBangumiAccountStateAsync(
            forceRefresh: false);
    }

    private void SettingsView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _bangumiAccount.Changed -= BangumiAccount_Changed;
        AppSettingsStore.Changed -= AppSettingsStore_Changed;
    }

    private void AppSettingsStore_Changed(
        object? sender,
        EventArgs e)
    {
        if (_isSynchronizing)
            return;

        DispatcherQueue.TryEnqueue(
            SyncControlsFromSettings);
    }

    private void SyncControlsFromSettings()
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

            var settings = AppSettingsStore.Current;
            LanguagePicker.SelectedIndex = settings.Language switch
            {
                AppLanguagePreference.SimplifiedChinese => 1,
                AppLanguagePreference.Japanese => 2,
                AppLanguagePreference.English => 3,
                _ => 0
            };

            MetadataAutoScrapeToggle.IsOn =
                settings.MetadataAutoScrapeOnScan;
            MetadataArtworkToggle.IsOn =
                settings.MetadataArtworkEnrichment;
            AutoPlayNextToggle.IsOn =
                settings.AutoPlayNextEpisode;
            RememberPlaybackRateToggle.IsOn =
                settings.RememberPlaybackRate;
            RememberSubtitleTrackToggle.IsOn =
                settings.RememberSubtitleTrack;

            DefaultPlaybackRateCombo.SelectedIndex =
                FindPlaybackRateIndex(
                    settings.DefaultPlaybackRate);
            PreferredAudioLanguageCombo.SelectedIndex =
                LanguagePreferenceIndex(
                    settings.PreferredAudioLanguage);
            PreferredSubtitleLanguageCombo.SelectedIndex =
                LanguagePreferenceIndex(
                    settings.PreferredSubtitleLanguage);

            PrimarySubtitlePositionSettingsSlider.Value =
                Math.Clamp(
                    settings.PrimarySubtitleVerticalPosition,
                    0d,
                    90d);
            SecondarySubtitlePositionSettingsSlider.Value =
                Math.Clamp(
                    settings.SecondarySubtitleVerticalPosition,
                    0d,
                    90d);
            PrimarySubtitleOpacitySettingsSlider.Value =
                Math.Clamp(
                    settings.PrimarySubtitleBackgroundOpacity,
                    0d,
                    100d);
            SecondarySubtitleOpacitySettingsSlider.Value =
                Math.Clamp(
                    settings.SecondarySubtitleBackgroundOpacity,
                    0d,
                    100d);

            UpdateSubtitleSettingValueText();
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void SettingsSectionList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (GeneralSettingsPanel is null)
            return;

        var index = Math.Max(
            0,
            SettingsSectionList.SelectedIndex);

        GeneralSettingsPanel.Visibility =
            index == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        MetadataSettingsPanel.Visibility =
            index == 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        PlaybackSettingsPanel.Visibility =
            index == 2
                ? Visibility.Visible
                : Visibility.Collapsed;
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

    private void AppearanceCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
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

    private async void LanguagePicker_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isSynchronizing ||
            LanguagePicker.SelectedIndex < 0)
        {
            return;
        }

        var preference = LanguagePicker.SelectedIndex switch
        {
            1 => AppLanguagePreference.SimplifiedChinese,
            2 => AppLanguagePreference.Japanese,
            3 => AppLanguagePreference.English,
            _ => AppLanguagePreference.System
        };

        LanguagePicker.IsEnabled = false;
        var switched =
            await _localization.SwitchLanguageAsync(
                preference);
        LanguagePicker.IsEnabled = true;
        if (switched)
            return;

        SyncControlsFromSettings();
    }

    private void MetadataAutoScrapeToggle_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(settings =>
            settings with
            {
                MetadataAutoScrapeOnScan =
                    MetadataAutoScrapeToggle.IsOn
            });
    }

    private void MetadataArtworkToggle_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(settings =>
            settings with
            {
                MetadataArtworkEnrichment =
                    MetadataArtworkToggle.IsOn
            });
    }

    private async void MetadataRescrapeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_metadataMaintenanceRunning)
            return;

        var sources = MediaSourceStore.Default
            .Snapshot()
            .Where(static source => source.Enabled)
            .Where(source =>
                MediaCatalogStore.Default
                    .SnapshotForSource(source.Id)
                    .Count > 0)
            .ToArray();

        if (sources.Length == 0)
        {
            MetadataActionStatusText.Text =
                T("Settings_MetadataNoMedia");
            return;
        }

        SetMetadataMaintenanceBusy(true);
        var processed = 0;
        var failed = 0;

        try
        {
            foreach (var source in sources)
            {
                MetadataActionStatusText.Text =
                    string.Format(
                        T("Settings_MetadataRescrapeRunningFormat"),
                        source.DisplayName);

                try
                {
                    var result =
                        await MediaScanCoordinator.Default
                            .StartMetadataAsync(source);

                    if (result.Status == MediaScanStatus.Completed)
                    {
                        processed +=
                            Math.Max(
                                result.MetadataProcessed,
                                result.MetadataTotal);
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            MetadataActionStatusText.Text =
                failed == 0
                    ? string.Format(
                        T("Settings_MetadataRescrapeCompleteFormat"),
                        processed)
                    : string.Format(
                        T("Settings_MetadataRescrapePartialFormat"),
                        processed,
                        failed);
        }
        finally
        {
            SetMetadataMaintenanceBusy(false);
        }
    }

    private async void MetadataClearCacheButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_metadataMaintenanceRunning)
            return;

        var scanning = MediaSourceStore.Default
            .Snapshot()
            .Any(source =>
                MediaScanCoordinator.Default.IsScanning(
                    source.Id));

        if (scanning)
        {
            MetadataActionStatusText.Text =
                T("Settings_MetadataBusy");
            return;
        }

        SetMetadataMaintenanceBusy(true);
        MetadataActionStatusText.Text =
            T("Settings_MetadataClearingCache");

        try
        {
            await MediaScanCoordinator
                .ClearMetadataCacheAsync();
            MetadataActionStatusText.Text =
                T("Settings_MetadataCacheCleared");
        }
        finally
        {
            SetMetadataMaintenanceBusy(false);
        }
    }

    private void SetMetadataMaintenanceBusy(bool busy)
    {
        _metadataMaintenanceRunning = busy;
        MetadataRescrapeButton.IsEnabled = !busy;
        MetadataClearCacheButton.IsEnabled = !busy;
        MetadataAutoScrapeToggle.IsEnabled = !busy;
        MetadataArtworkToggle.IsEnabled = !busy;
    }

    private void AutoPlayNextToggle_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(settings =>
            settings with
            {
                AutoPlayNextEpisode =
                    AutoPlayNextToggle.IsOn
            });
    }

    private void RememberPlaybackRateToggle_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(settings =>
            settings with
            {
                RememberPlaybackRate =
                    RememberPlaybackRateToggle.IsOn
            });
    }

    private void DefaultPlaybackRateCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isSynchronizing ||
            DefaultPlaybackRateCombo.SelectedIndex < 0 ||
            DefaultPlaybackRateCombo.SelectedIndex >=
                PlaybackRates.Length)
        {
            return;
        }

        var rate =
            PlaybackRates[
                DefaultPlaybackRateCombo.SelectedIndex];
        AppSettingsStore.Update(settings =>
            settings with
            {
                DefaultPlaybackRate = rate
            });
    }

    private void PreferredAudioLanguageCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isSynchronizing ||
            PreferredAudioLanguageCombo.SelectedIndex < 0)
        {
            return;
        }

        AppSettingsStore.Update(settings =>
            settings with
            {
                PreferredAudioLanguage =
                    LanguagePreferenceCode(
                        PreferredAudioLanguageCombo.SelectedIndex)
            });
    }

    private void PreferredSubtitleLanguageCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isSynchronizing ||
            PreferredSubtitleLanguageCombo.SelectedIndex < 0)
        {
            return;
        }

        AppSettingsStore.Update(settings =>
            settings with
            {
                PreferredSubtitleLanguage =
                    LanguagePreferenceCode(
                        PreferredSubtitleLanguageCombo.SelectedIndex)
            });
    }

    private void RememberSubtitleTrackToggle_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(settings =>
            settings with
            {
                RememberSubtitleTrack =
                    RememberSubtitleTrackToggle.IsOn
            });
    }

    private void PrimarySubtitlePositionSettingsSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(settings =>
            settings with
            {
                PrimarySubtitleVerticalPosition =
                    Math.Clamp(e.NewValue, 0d, 90d)
            });
        UpdateSubtitleSettingValueText();
    }

    private void SecondarySubtitlePositionSettingsSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(settings =>
            settings with
            {
                SecondarySubtitleVerticalPosition =
                    Math.Clamp(e.NewValue, 0d, 90d)
            });
        UpdateSubtitleSettingValueText();
    }

    private void PrimarySubtitleOpacitySettingsSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(settings =>
            settings with
            {
                PrimarySubtitleBackgroundOpacity =
                    Math.Clamp(e.NewValue, 0d, 100d)
            });
        UpdateSubtitleSettingValueText();
    }

    private void SecondarySubtitleOpacitySettingsSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isSynchronizing)
            return;

        AppSettingsStore.Update(settings =>
            settings with
            {
                SecondarySubtitleBackgroundOpacity =
                    Math.Clamp(e.NewValue, 0d, 100d)
            });
        UpdateSubtitleSettingValueText();
    }

    private void UpdateSubtitleSettingValueText()
    {
        if (PrimarySubtitlePositionSettingsValue is null)
            return;

        PrimarySubtitlePositionSettingsValue.Text =
            $"{PrimarySubtitlePositionSettingsSlider.Value:0}%";
        SecondarySubtitlePositionSettingsValue.Text =
            $"{SecondarySubtitlePositionSettingsSlider.Value:0}%";
        PrimarySubtitleOpacitySettingsValue.Text =
            $"{PrimarySubtitleOpacitySettingsSlider.Value:0}%";
        SecondarySubtitleOpacitySettingsValue.Text =
            $"{SecondarySubtitleOpacitySettingsSlider.Value:0}%";
    }

    private static int FindPlaybackRateIndex(double value)
    {
        var bestIndex = 0;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < PlaybackRates.Length; i++)
        {
            var distance =
                Math.Abs(PlaybackRates[i] - value);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static int LanguagePreferenceIndex(
        string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "ja" => 1,
            "zh" => 2,
            "en" => 3,
            _ => 0
        };

    private static string LanguagePreferenceCode(
        int index) =>
        index switch
        {
            1 => "ja",
            2 => "zh",
            3 => "en",
            _ => "auto"
        };

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Settings");
        PageSubtitle.Text = T("Settings_Subtitle");

        SettingsSectionList.ItemsSource = new[]
        {
            T("Settings_General"),
            T("Settings_Metadata"),
            T("Settings_Playback")
        };

        LanguageSectionTitle.Text =
            T("Settings_LanguageAppearance");
        LanguageTitle.Text = T("Settings_Language");
        LanguageDescription.Text =
            T("Settings_LanguageDescription");
        LanguagePicker.ItemsSource = new[]
        {
            T("Settings_LanguageSystem"),
            "简体中文",
            "日本語",
            "English"
        };

        AppearanceTitle.Text = T("Settings_Appearance");
        AppearanceDescription.Text =
            T("Settings_AppearanceDescription");
        AppearanceCombo.ItemsSource = new[]
        {
            T("Settings_AppearanceSystem"),
            T("Settings_AppearanceLight"),
            T("Settings_AppearanceDark")
        };

        BangumiSectionTitle.Text =
            T("Bangumi_SettingsSection");
        BangumiAccountTitle.Text =
            T("Bangumi_Account");
        BangumiAccountDescription.Text =
            T("Bangumi_AccountSettingsDescription");
        BangumiConnectButton.Content =
            T("Bangumi_Connect");
        BangumiDisconnectButton.Content =
            T("Bangumi_Disconnect");

        MetadataAutomationSectionTitle.Text =
            T("Settings_MetadataAutomation");
        MetadataAutoScrapeLabel.Text =
            T("Settings_MetadataAutoScrape");
        MetadataAutoScrapeDescription.Text =
            T("Settings_MetadataAutoScrapeDescription");
        MetadataArtworkLabel.Text =
            T("Settings_MetadataArtwork");
        MetadataArtworkDescription.Text =
            T("Settings_MetadataArtworkDescription");

        MetadataMaintenanceSectionTitle.Text =
            T("Settings_MetadataMaintenance");
        MetadataRescrapeLabel.Text =
            T("Settings_MetadataRescrape");
        MetadataRescrapeDescription.Text =
            T("Settings_MetadataRescrapeDescription");
        MetadataRescrapeButton.Content =
            T("Settings_MetadataRescrapeButton");
        MetadataClearCacheLabel.Text =
            T("Settings_MetadataClearCache");
        MetadataClearCacheDescription.Text =
            T("Settings_MetadataClearCacheDescription");
        MetadataClearCacheButton.Content =
            T("Settings_MetadataClearCacheButton");

        PlaybackBehaviorSectionTitle.Text =
            T("Settings_PlaybackBehavior");
        AutoPlayNextLabel.Text =
            T("Settings_AutoPlayNext");
        AutoPlayNextDescription.Text =
            T("Settings_AutoPlayNextDescription");
        RememberPlaybackRateLabel.Text =
            T("Settings_RememberPlaybackRate");
        RememberPlaybackRateDescription.Text =
            T("Settings_RememberPlaybackRateDescription");
        DefaultPlaybackRateLabel.Text =
            T("Settings_DefaultPlaybackRate");
        DefaultPlaybackRateDescription.Text =
            T("Settings_DefaultPlaybackRateDescription");
        DefaultPlaybackRateCombo.ItemsSource =
            PlaybackRates
                .Select(static rate => $"{rate:0.##}×")
                .ToArray();

        PlaybackTracksSectionTitle.Text =
            T("Settings_TracksAndSubtitles");
        PreferredAudioLanguageLabel.Text =
            T("Settings_PreferredAudioLanguage");
        PreferredAudioLanguageDescription.Text =
            T("Settings_PreferredAudioLanguageDescription");
        PreferredSubtitleLanguageLabel.Text =
            T("Settings_PreferredSubtitleLanguage");
        PreferredSubtitleLanguageDescription.Text =
            T("Settings_PreferredSubtitleLanguageDescription");
        RememberSubtitleTrackLabel.Text =
            T("Settings_RememberSubtitleTrack");
        RememberSubtitleTrackDescription.Text =
            T("Settings_RememberSubtitleTrackDescription");

        var trackLanguages = new[]
        {
            T("Settings_LanguageAuto"),
            "日本語",
            "简体中文",
            "English"
        };
        PreferredAudioLanguageCombo.ItemsSource =
            trackLanguages;
        PreferredSubtitleLanguageCombo.ItemsSource =
            trackLanguages.ToArray();

        SubtitleStyleSectionTitle.Text =
            T("Settings_SubtitleStyle");
        PrimarySubtitlePositionSettingsLabel.Text =
            T("Playback_PrimarySubtitlePosition");
        SecondarySubtitlePositionSettingsLabel.Text =
            T("Playback_SecondarySubtitlePosition");
        PrimarySubtitleOpacitySettingsLabel.Text =
            T("Playback_PrimarySubtitleOpacity");
        SecondarySubtitleOpacitySettingsLabel.Text =
            T("Playback_SecondarySubtitleOpacity");
    }
}
