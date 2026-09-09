using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;

namespace Eizo.Views;

public sealed partial class SourcesView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;

    private readonly MediaSourceStore _sources =
        MediaSourceStore.Default;

    private readonly MediaCatalogStore _catalog =
        MediaCatalogStore.Default;

    private readonly MediaCredentialStore _credentials =
        MediaCredentialStore.Default;

    public SourcesView()
    {
        InitializeComponent();
        ApplyText();

        Loaded += SourcesView_Loaded;
        Unloaded += SourcesView_Unloaded;

        RefreshSources();
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Sources");
        PageSubtitle.Text = T("Sources_Subtitle");
        AddSourceButton.Content = T("Source_Add");

        SectionList.ItemsSource =
            new[] { T("Sources_TabSources") };
    }

    private void SourcesView_Loaded(object sender, RoutedEventArgs e)
    {
        _sources.Changed -= Sources_Changed;
        _sources.Changed += Sources_Changed;

        _catalog.Changed -= Catalog_Changed;
        _catalog.Changed += Catalog_Changed;

        RefreshSources();
    }

    private void SourcesView_Unloaded(object sender, RoutedEventArgs e)
    {
        _sources.Changed -= Sources_Changed;
        _catalog.Changed -= Catalog_Changed;
    }

    private void Sources_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshSources);

    private void Catalog_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshSources);

    private void RefreshSources()
    {
        if (SourceList is null)
            return;

        SourceList.ItemsSource = _sources
            .Snapshot()
            .Where(source => source.Enabled)
            .OrderBy(source => source.IsBuiltIn ? 0 : 1)
            .ThenBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(CreateSourceItem)
            .ToArray();
    }

    private SourceItemModel CreateSourceItem(MediaSourceDefinition source)
    {
        var items = _catalog.SnapshotForSource(source.Id);
        var size = items
            .Select(item => item.Location?.SizeBytes ?? 0L)
            .Where(value => value > 0)
            .Sum();

        var summaryParts = new List<string>();

        if (source.Kind == MediaSourceKind.WebDav)
            summaryParts.Add(T("Source_WebDAV"));

        summaryParts.Add(
            string.Format(
                T("Sources_VideoCountFormat"),
                items.Count));

        if (size > 0)
            summaryParts.Add(FormatBytes(size));

        if (source.LastScanUtc is { } lastScan)
        {
            summaryParts.Add(
                string.Format(
                    T("Sources_LastScanFormat"),
                    lastScan.ToLocalTime().ToString("g")));
        }

        var name = source.IsBuiltIn
            ? T("Sources_OpenedLocalFiles")
            : source.DisplayName;

        var kindLabel = source.Kind switch
        {
            MediaSourceKind.WebDav => T("Source_WebDAV"),
            MediaSourceKind.Local => T("Source_Local"),
            _ => source.Kind.ToString()
        };

        return new SourceItemModel(
            source.Id,
            name,
            string.Join(" · ", summaryParts),
            kindLabel,
            Removable: !source.IsBuiltIn);
    }

    private void SourceList_ContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem container ||
            args.Item is not SourceItemModel item)
        {
            return;
        }

        var source = _sources.Find(item.Id);
        if (source is null)
            return;

        var flyout = new MenuFlyout();

        if (!source.IsBuiltIn)
        {
            var scan = new MenuFlyoutItem
            {
                Text = T("Source_ScanNow"),
                Icon = new FontIcon { Glyph = "\uE72C" },
                Tag = source.Id
            };
            scan.Click += ScanSourceMenuItem_Click;
            flyout.Items.Add(scan);
        }

        if (source.Kind == MediaSourceKind.WebDav)
        {
            var test = new MenuFlyoutItem
            {
                Text = T("Sources_TestConnection"),
                Icon = new FontIcon { Glyph = "\uE774" },
                Tag = source.Id
            };
            test.Click += TestConnectionMenuItem_Click;
            flyout.Items.Add(test);
        }

        if (item.Removable)
        {
            if (flyout.Items.Count > 0)
                flyout.Items.Add(new MenuFlyoutSeparator());

            var remove = new MenuFlyoutItem
            {
                Text = T("Common_Remove"),
                Icon = new FontIcon { Glyph = "\uE711" },
                Tag = source.Id
            };
            remove.Click += RemoveSourceMenuItem_Click;
            flyout.Items.Add(remove);
        }

        container.ContextFlyout =
            flyout.Items.Count > 0
                ? flyout
                : null;
        container.Tag = item;
    }

    private async void ScanSourceMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string sourceId })
            return;

        var source = _sources.Find(sourceId);
        if (source is null)
            return;

        try
        {
            if (source.Kind == MediaSourceKind.Local &&
                !string.IsNullOrWhiteSpace(source.AccessToken) &&
                StorageApplicationPermissions.FutureAccessList.ContainsItem(
                    source.AccessToken))
            {
                _ = await StorageApplicationPermissions
                    .FutureAccessList
                    .GetFolderAsync(source.AccessToken);
            }

            await _catalog.ScanSourceAsync(source);
        }
        catch (MediaSourceException exception)
        {
            await ShowMessageAsync(
                T("Sources_ScanFailed"),
                FormatSourceError(exception.ErrorCode, exception.Message));
        }
        catch (Exception exception)
        {
            await ShowMessageAsync(
                T("Sources_ScanFailed"),
                exception.Message);
        }
    }

    private async void TestConnectionMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string sourceId })
            return;

        var source = _sources.Find(sourceId);
        if (source is null ||
            !MediaSourceProviderRegistry.TryGet(
                source.Kind,
                out var provider))
        {
            return;
        }

        var result = await provider.TestConnectionAsync(source);

        await ShowMessageAsync(
            result.IsAvailable
                ? T("Sources_ConnectionSucceeded")
                : T("Sources_ConnectionFailed"),
            result.IsAvailable
                ? source.DisplayName
                : FormatSourceError(
                    result.ErrorCode,
                    result.Detail));
    }

    private void RemoveSourceMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string sourceId })
            return;

        var source = _sources.Find(sourceId);
        if (source is null || source.IsBuiltIn)
            return;

        _catalog.RemoveSourceItems(sourceId);
        _sources.Remove(sourceId);

        if (source.Kind == MediaSourceKind.WebDav)
            _credentials.RemoveWebDav(sourceId);

        if (!string.IsNullOrWhiteSpace(source.AccessToken) &&
            StorageApplicationPermissions.FutureAccessList.ContainsItem(
                source.AccessToken))
        {
            StorageApplicationPermissions.FutureAccessList.Remove(
                source.AccessToken);
        }
    }

    private void AddSourceButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();

        var local = new MenuFlyoutItem
        {
            Text = T("Sources_AddLocalFolder"),
            Icon = new FontIcon { Glyph = "\uE8B7" }
        };
        local.Click += async (_, _) => await AddLocalSourceAsync();
        flyout.Items.Add(local);

        var webDav = new MenuFlyoutItem
        {
            Text = T("Sources_AddWebDav"),
            Icon = new FontIcon { Glyph = "\uE753" }
        };
        webDav.Click += async (_, _) => await AddWebDavSourceAsync();
        flyout.Items.Add(webDav);

        flyout.ShowAt(AddSourceButton);
    }

    private async Task AddLocalSourceAsync()
    {
        if (App.MainWindow is null)
            return;

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary
        };
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
            App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
            return;

        try
        {
            var source = _sources.AddLocalFolder(
                folder.Path,
                folder.Name);

            StorageApplicationPermissions.FutureAccessList.AddOrReplace(
                source.Id,
                folder);

            source = _sources.AddLocalFolder(
                folder.Path,
                folder.Name,
                accessToken: source.Id);

            await _catalog.ScanSourceAsync(source);
        }
        catch (Exception exception)
        {
            await ShowMessageAsync(
                T("Sources_AddLocalFailed"),
                exception.Message);
        }
    }

    private async Task AddWebDavSourceAsync()
    {
        var displayName = new TextBox
        {
            Header = T("Sources_DisplayName"),
            PlaceholderText = "NAS"
        };
        var address = new TextBox
        {
            Header = T("Sources_Address"),
            PlaceholderText = "https://example.com/dav/"
        };
        var userName = new TextBox
        {
            Header = T("Sources_Username")
        };
        var password = new PasswordBox
        {
            Header = T("Sources_Password")
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 420
        };
        panel.Children.Add(displayName);
        panel.Children.Add(address);
        panel.Children.Add(userName);
        panel.Children.Add(password);

        if (XamlRoot is null)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = T("Sources_WebDavDialogTitle"),
            Content = panel,
            PrimaryButtonText = T("Sources_ConnectAndAdd"),
            CloseButtonText = T("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        if (!Uri.TryCreate(
                address.Text.Trim(),
                UriKind.Absolute,
                out var rootUri) ||
            (rootUri.Scheme != Uri.UriSchemeHttp &&
             rootUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(rootUri.UserInfo))
        {
            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                T("Sources_InvalidAddress"));
            return;
        }

        var normalizedRoot = NormalizeWebDavUri(rootUri);
        var name = string.IsNullOrWhiteSpace(displayName.Text)
            ? normalizedRoot.Host
            : displayName.Text.Trim();
        var normalizedUser = userName.Text.Trim();
        var sourceId = MediaSourceStore.BuildWebDavSourceId(
            normalizedRoot,
            normalizedUser);

        var existing = _sources.Find(sourceId);
        var previousCredential = existing is null
            ? null
            : _credentials.GetWebDav(existing);

        string? credentialKey = null;

        try
        {
            credentialKey = _credentials.SaveWebDav(
                sourceId,
                normalizedUser,
                password.Password);

            var temporarySource = new MediaSourceDefinition(
                sourceId,
                MediaSourceKind.WebDav,
                name,
                normalizedRoot.AbsoluteUri,
                UserName: normalizedUser,
                CredentialKey: credentialKey);

            if (!MediaSourceProviderRegistry.TryGet(
                    MediaSourceKind.WebDav,
                    out var provider))
            {
                throw new MediaSourceException(
                    "ProviderUnavailable",
                    "WebDAV provider is unavailable.");
            }

            var test = await provider.TestConnectionAsync(
                temporarySource);

            if (!test.IsAvailable)
            {
                RestoreCredential(
                    sourceId,
                    previousCredential);

                await ShowMessageAsync(
                    T("Sources_ConnectionFailed"),
                    FormatSourceError(
                        test.ErrorCode,
                        test.Detail));
                return;
            }

            var source = _sources.AddWebDav(
                name,
                normalizedRoot,
                normalizedUser,
                credentialKey);

            await _catalog.ScanSourceAsync(source);

            await ShowMessageAsync(
                T("Sources_ConnectionSucceeded"),
                string.Format(
                    T("Sources_WebDavAddedFormat"),
                    source.DisplayName,
                    _catalog.SnapshotForSource(source.Id).Count));
        }
        catch (Exception exception)
        {
            RestoreCredential(
                sourceId,
                previousCredential);

            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                exception is MediaSourceException sourceException
                    ? FormatSourceError(
                        sourceException.ErrorCode,
                        sourceException.Message)
                    : exception.Message);
        }
    }

    private void RestoreCredential(
        string sourceId,
        MediaCredentialSnapshot? previous)
    {
        if (previous is null)
        {
            _credentials.RemoveWebDav(sourceId);
            return;
        }

        _credentials.SaveWebDav(
            sourceId,
            previous.UserName,
            previous.Password);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        if (XamlRoot is null)
            return;

        await new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = T("Common_Close")
        }.ShowAsync();
    }

    private string FormatSourceError(
        string? errorCode,
        string? detail)
    {
        var code = errorCode switch
        {
            "AuthenticationFailed" => T("Sources_ErrorAuthentication"),
            "Forbidden" => T("Sources_ErrorForbidden"),
            "NotFound" => T("Sources_ErrorNotFound"),
            "Timeout" => T("Sources_ErrorTimeout"),
            "NetworkError" => T("Sources_ErrorNetwork"),
            "InvalidWebDavResponse" => T("Sources_ErrorInvalidResponse"),
            _ => T("Sources_ErrorGeneric")
        };

        return string.IsNullOrWhiteSpace(detail)
            ? code
            : $"{code}\n{detail}";
    }

    private static Uri NormalizeWebDavUri(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
            builder.Path += "/";

        return builder.Uri;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0d, bytes);
        var unit = 0;

        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.#} {units[unit]}";
    }
}
