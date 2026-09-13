using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

internal static class BangumiAccountDialogService
{
    private static readonly Uri AccessTokenUri =
        new(
            "https://next.bgm.tv/demo/access-token",
            UriKind.Absolute);

    public static async Task<bool> ShowConnectAsync(
        XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);

        var localization = AppLocalizationService.Default;
        string T(string key) =>
            localization.GetString(key);

        var tokenBox = new PasswordBox
        {
            PlaceholderText = T("Bangumi_AccountTokenPlaceholder"),
            MinWidth = 360,
        };

        var content = new StackPanel
        {
            Spacing = 10,
        };

        content.Children.Add(
            new TextBlock
            {
                Text = T("Bangumi_AccountTokenHelp"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520,
            });

        content.Children.Add(
            new HyperlinkButton
            {
                Content = T("Bangumi_OpenTokenPage"),
                NavigateUri = AccessTokenUri,
                Padding = new Thickness(0, 4, 0, 4),
            });

        content.Children.Add(tokenBox);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = T("Bangumi_ConnectAccount"),
            Content = content,
            PrimaryButtonText = T("Bangumi_Connect"),
            CloseButtonText = T("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return false;

        var token = tokenBox.Password?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            await ShowMessageAsync(
                xamlRoot,
                T("Bangumi_AccountTokenRequired"),
                T("Bangumi_ConnectFailed"));
            return false;
        }

        try
        {
            await BangumiAccountService.Default.ConnectAsync(token);
            return true;
        }
        catch
        {
            await ShowMessageAsync(
                xamlRoot,
                T("Bangumi_AccountTokenInvalid"),
                T("Bangumi_ConnectFailed"));
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
