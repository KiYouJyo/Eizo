using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

internal static class BangumiAccountDialogService
{
    public static async Task<bool> ShowConnectAsync(
        XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        var localization = AppLocalizationService.Default;
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = localization.GetString("Bangumi_ConnectAccount"),
            Content = new TextBlock
            {
                Text = localization.GetString("Bangumi_BrowserLoginHelp"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520,
            },
            PrimaryButtonText = localization.GetString("Bangumi_BrowserLogin"),
            CloseButtonText = localization.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return false;

        try
        {
            await BangumiOAuthService.Default.StartAsync();
            return false;
        }
        catch
        {
            await ShowMessageAsync(
                xamlRoot,
                localization.GetString("Bangumi_BrowserLoginFailed"),
                localization.GetString("Bangumi_ConnectFailed"));
            return false;
        }
    }

    public static async Task<bool> ConfirmDisconnectAsync(
        XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);

        var localization = AppLocalizationService.Default;
        string T(string key) =>
            localization.GetString(key);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = T("Bangumi_Disconnect"),
            Content = new TextBlock
            {
                Text = T("Bangumi_DisconnectConfirm"),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = T("Bangumi_Disconnect"),
            CloseButtonText = T("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() !=
            ContentDialogResult.Primary)
        {
            return false;
        }

        BangumiAccountService.Default.Disconnect();
        return true;
    }

    private static async Task ShowMessageAsync(
        XamlRoot xamlRoot,
        string message,
        string title)
    {
        var localization = AppLocalizationService.Default;

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
            },
            CloseButtonText =
                localization.GetString("Common_Close"),
        };

        await dialog.ShowAsync();
    }
}
