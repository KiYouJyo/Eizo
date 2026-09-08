using Eizo.Controls;
using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class SourcesView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;

    public SourcesView()
    {
        InitializeComponent();
        ApplyText();

        SourceList.ItemsSource = new[]
        {
            new SourceItemModel(T("Source_Local") + " · Anime", "1,248 · 2.8 TB", "local"),
            new SourceItemModel("WebDAV · NAS", "3,612 · 8.4 TB", "webdav"),
            new SourceItemModel("OneDrive · Personal", "824 · 1.2 TB", "onedrive"),
            new SourceItemModel("Google Drive", "—", "gdrive")
        };
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Sources");
        PageSubtitle.Text = T("Sources_Subtitle");
        AddSourceButton.Content = T("Source_Add");
        SectionList.ItemsSource = new[] { T("Sources_TabSources"), T("Sources_TabScan") };
    }

    private void SourceList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem container || args.Item is not SourceItemModel item) return;

        var flyout = new MenuFlyout();
        AddMenuItem(flyout, T("Common_Edit"), "\uE70F");
        AddMenuItem(flyout, T("Source_ScanNow"), "\uE72C");
        AddMenuItem(flyout, T("Source_ScanHistory"), "\uE81C");
        flyout.Items.Add(new MenuFlyoutSeparator());
        AddMenuItem(flyout, T("Source_Disconnect"), "\uE8CD");
        AddMenuItem(flyout, T("Common_Remove"), "\uE711");

        container.ContextFlyout = flyout;
        container.Tag = item;
    }

    private static void AddMenuItem(MenuFlyout flyout, string text, string glyph) =>
        flyout.Items.Add(new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph } });

    private async void AddSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = new ComboBox
        {
            Header = T("Sources_Provider"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { T("Source_Local"), "WebDAV", "OneDrive", "Google Drive" },
            SelectedIndex = 1
        };

        TransientComboBoxTheme.SetApply(provider, true);

        var content = new StackPanel { Spacing = 12, MinWidth = 360 };
        content.Children.Add(provider);
        content.Children.Add(new TextBox { Header = T("Sources_DisplayName"), PlaceholderText = "NAS" });
        content.Children.Add(new TextBox { Header = T("Sources_Address"), PlaceholderText = "https://" });
        content.Children.Add(new TextBox { Header = T("Sources_Username") });
        content.Children.Add(new PasswordBox { Header = T("Sources_Password") });

        await new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = T("Source_Add"),
            PrimaryButtonText = T("Common_Add"),
            CloseButtonText = T("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = content
        }.ShowAsync();
    }
}
