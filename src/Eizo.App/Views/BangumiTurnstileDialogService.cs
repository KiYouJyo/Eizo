using Eizo.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

internal static class BangumiTurnstileDialogService
{
    private const string CallbackUri =
        "eizo://bangumi-turnstile-callback";
    private static readonly HttpClient ProbeClient =
        CreateProbeClient();

    public static async Task<string?> AcquireAsync(
        XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);

        var verificationUri = BuildVerificationUri();
        if (!await IsAvailableAsync(verificationUri))
            return null;

        var localization = AppLocalizationService.Default;
        var webView = new WebView2
        {
            MinWidth = 480,
            MinHeight = 420,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        string? token = null;
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = localization.GetString(
                "Bangumi_TurnstileTitle"),
            Content = webView,
            CloseButtonText = localization.GetString(
                "Common_Cancel"),
        };

        webView.NavigationStarting += (_, args) =>
        {
            if (!Uri.TryCreate(
                    args.Uri,
                    UriKind.Absolute,
                    out var uri) ||
                !string.Equals(
                    uri.Scheme,
                    "eizo",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    uri.Host,
                    "bangumi-turnstile-callback",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            args.Cancel = true;
            token = ReadQueryValue(
                uri.Query,
                "token");
            dialog.Hide();
        };

        webView.Source = verificationUri;
        await dialog.ShowAsync();
        webView.Close();
        return string.IsNullOrWhiteSpace(token)
            ? null
            : token;
    }

    public static async Task<bool> IsAvailableAsync()
    {
        return await IsAvailableAsync(
            BuildVerificationUri());
    }

    private static async Task<bool> IsAvailableAsync(
        Uri verificationUri)
    {
        try
        {
            using var response = await ProbeClient.GetAsync(
                verificationUri,
                HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static Uri BuildVerificationUri()
    {
        var builder = new UriBuilder(
            "https://next.bgm.tv/p1/turnstile");
        builder.Query =
            "theme=auto&redirect_uri=" +
            Uri.EscapeDataString(CallbackUri);
        return builder.Uri;
    }

    private static string? ReadQueryValue(
        string query,
        string name)
    {
        foreach (var pair in query.TrimStart('?').Split(
                     '&',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 &&
                string.Equals(
                    Uri.UnescapeDataString(parts[0]),
                    name,
                    StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }

    private static HttpClient CreateProbeClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "KiYouJyo/Eizo/0.4.5 (Windows) (https://github.com/KiYouJyo/Eizo)");
        return client;
    }
}
