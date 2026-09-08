using Eizo.Localization;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class AboutView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;

    public AboutView()
    {
        InitializeComponent();
        ApplyText();
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_About");
        AboutDescription.Text = T("App_Description");

        DisplayVersionLabel.Text = T("About_DisplayVersion");
        InternalVersionLabel.Text = T("About_InternalVersion");
        ArchitectureLabel.Text = T("About_Architecture");
        ChannelLabel.Text = T("About_Channel");
        PublisherLabel.Text = T("About_Publisher");
        TechLabel.Text = T("About_TechStack");

        UpdateTitle.Text = T("About_UpdateManagement");
        ApplicationUpdateLabel.Text = T("About_ApplicationUpdate");
        CurrentVersionLabel.Text = T("About_CurrentVersion");
        AvailableVersionLabel.Text = T("About_AvailableVersion");
        UpdateSourceLabel.Text = T("About_UpdateSource");
        UpdateStatusLabel.Text = T("About_Status");
        NotCheckedText.Text = T("About_NotChecked");
        ReleaseNotesButton.Content = T("About_ReleaseNotes");
        CheckUpdateButton.Content = T("About_CheckUpdates");

        IndependentComponentsTitle.Text = T("About_IndependentComponents");
        PlayerCoreTitle.Text = T("About_PlayerCore");
        IndependentUpdateLabel.Text = T("About_IndependentUpdate");
        NotCheckedComponentText.Text = T("About_NotChecked");
        CoreReleaseNotesButton.Content = T("About_ReleaseNotes");
        CoreCheckUpdateButton.Content = T("About_CheckUpdates");

        ProjectTitle.Text = T("About_ProjectOpenSource");
        GitHubDescription.Text = T("About_GitHubDescription");
        ReleasesDescription.Text = T("About_ReleasesDescription");
        PrivacyTitle.Text = T("About_Privacy");
        PrivacyDescription.Text = T("About_PrivacyDescription");
        OpenRepositoryButton.Content = T("About_OpenRepository");
        OpenReleasesButton.Content = T("About_OpenReleases");
        OpenPrivacyButton.Content = T("About_OpenPrivacy");
    }
}
