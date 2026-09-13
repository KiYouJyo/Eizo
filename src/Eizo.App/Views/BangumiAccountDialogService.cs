using System.Net;
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
            SecondaryButtonText = localization.GetString("Bangumi_ManualLogin"),
            CloseButtonText = localization.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
            return await ShowManualConnectAsync(xamlRoot);
        if (result != ContentDialogResult.Primary) return false;
        try
        {
            await BangumiOAuthService.Default.StartAsync();
            return false;
        }
        catch
        {
            await ShowMessageAsync(xamlRoot,
                localization.GetString("Bangumi_BrowserLoginFailed"),
                localization.GetString("Bangumi_ConnectFailed"));
            return false;
        }
    }

    private static async Task<bool> ShowManualConnectAsync(
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
            Title = T("Bangumi_ManualLogin"),
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
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await ShowMessageAsync(
                xamlRoot,
                T("Bangumi_AccountTokenInvalid"),
                T("Bangumi_ConnectFailed"));
            return false;
        }
        catch
        {
            await ShowMessageAsync(
                xamlRoot,
                T("Bangumi_AccountConnectNetworkError"),
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
