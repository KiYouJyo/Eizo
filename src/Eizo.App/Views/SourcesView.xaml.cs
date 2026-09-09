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
            var edit = new MenuFlyoutItem
            {
                Text = T("Common_Edit"),
                Icon = new FontIcon { Glyph = "\uE70F" },
                Tag = source.Id
            };
            edit.Click += EditWebDavMenuItem_Click;
            flyout.Items.Add(edit);

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

    private async void EditWebDavMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string sourceId })
            return;

        var source = _sources.Find(sourceId);
        if (source is not { Kind: MediaSourceKind.WebDav })
            return;

        await ShowWebDavEditorAsync(source);
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

    private async Task AddWebDavSourceAsync() =>
        await ShowWebDavEditorAsync(existingSource: null);

    private async Task ShowWebDavEditorAsync(
        MediaSourceDefinition? existingSource)
    {
        var existingCredential = existingSource is null
            ? null
            : _credentials.GetWebDav(existingSource);

        var displayName = new TextBox
        {
            Header = T("Sources_DisplayName"),
            PlaceholderText = "NAS",
            Text = existingSource?.DisplayName ?? string.Empty
        };

        var address = new TextBox
        {
            Header = T("Sources_Address"),
            PlaceholderText = "https://example.com/dav/",
            Text = existingSource?.RootLocation ?? string.Empty
        };

        var browseButton = new Button
        {
            Content = T("Common_Browse"),
            MinWidth = 88,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var addressRow = new Grid
        {
            ColumnSpacing = 8
        };
        addressRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        addressRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        addressRow.Children.Add(address);
        Grid.SetColumn(browseButton, 1);
        addressRow.Children.Add(browseButton);

        var userName = new TextBox
        {
            Header = T("Sources_Username"),
            Text = existingSource?.UserName ?? string.Empty
        };

        var password = new PasswordBox
        {
            Header = T("Sources_Password"),
            PlaceholderText = existingCredential is null
                ? string.Empty
                : T("Sources_PasswordKeepHint")
        };

        var editorStatus = new TextBlock
        {
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["MetadataText"]
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 460
        };
        panel.Children.Add(displayName);
        panel.Children.Add(addressRow);
        panel.Children.Add(userName);
        panel.Children.Add(password);
        panel.Children.Add(editorStatus);

        browseButton.Click += async (_, _) =>
            await BrowseWebDavFoldersAsync(
                address,
                userName,
                password,
                existingSource,
                editorStatus,
                browseButton);

        if (XamlRoot is null)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = existingSource is null
                ? T("Sources_WebDavDialogTitle")
                : T("Sources_EditWebDavTitle"),
            Content = panel,
            PrimaryButtonText = existingSource is null
                ? T("Sources_ConnectAndAdd")
                : T("Sources_SaveChanges"),
            CloseButtonText = T("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        await CommitWebDavEditorAsync(
            existingSource,
            displayName.Text,
            address.Text,
            userName.Text,
            password.Password);
    }

    private async Task CommitWebDavEditorAsync(
        MediaSourceDefinition? existingSource,
        string displayName,
        string address,
        string userName,
        string password)
    {
        if (!TryNormalizeWebDavUri(
                address,
                out var normalizedRoot))
        {
            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                T("Sources_InvalidAddress"));
            return;
        }

        var normalizedUser = userName.Trim();
        var name = string.IsNullOrWhiteSpace(displayName)
            ? normalizedRoot.Host
            : displayName.Trim();

        var credential = ResolveEditorCredential(
            existingSource,
            normalizedUser,
            password);

        var sourceId = MediaSourceStore.BuildWebDavSourceId(
            normalizedRoot,
            normalizedUser);

        var collision = _sources.Find(sourceId);
        if (collision is not null &&
            !string.Equals(
                collision.Id,
                existingSource?.Id,
                StringComparison.Ordinal))
        {
            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                T("Sources_SourceAlreadyExists"));
            return;
        }

        var temporarySource = new MediaSourceDefinition(
            sourceId,
            MediaSourceKind.WebDav,
            name,
            normalizedRoot.AbsoluteUri,
            UserName: normalizedUser,
            CredentialKey: credential is null
                ? null
                : "inline");

        var temporaryProvider =
            new WebDavMediaSourceProvider(
                new InlineWebDavCredentialProvider(
                    credential));

        var test = await temporaryProvider.TestConnectionAsync(
            temporarySource);

        if (!test.IsAvailable)
        {
            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                FormatSourceError(
                    test.ErrorCode,
                    test.Detail));
            return;
        }

        var previousTargetSource = _sources.Find(sourceId);
        var previousTargetCredential =
            previousTargetSource is null
                ? null
                : _credentials.GetWebDav(
                    previousTargetSource);

        try
        {
            var credentialKey = credential is null
                ? _credentials.SaveWebDav(
                    sourceId,
                    null,
                    null)
                : _credentials.SaveWebDav(
                    sourceId,
                    credential.UserName,
                    credential.Password);

            var savedSource = _sources.AddWebDav(
                name,
                normalizedRoot,
                normalizedUser,
                credentialKey);

            await _catalog.ScanSourceAsync(savedSource);

            if (existingSource is not null &&
                !string.Equals(
                    existingSource.Id,
                    savedSource.Id,
                    StringComparison.Ordinal))
            {
                _catalog.RemoveSourceItems(
                    existingSource.Id);
                _sources.Remove(
                    existingSource.Id);
                _credentials.RemoveWebDav(
                    existingSource.Id);
            }

            await ShowMessageAsync(
                T("Sources_ConnectionSucceeded"),
                string.Format(
                    existingSource is null
                        ? T("Sources_WebDavAddedFormat")
                        : T("Sources_WebDavUpdatedFormat"),
                    savedSource.DisplayName,
                    _catalog
                        .SnapshotForSource(savedSource.Id)
                        .Count));
        }
        catch (Exception exception)
        {
            RollBackWebDavTarget(
                sourceId,
                previousTargetSource,
                previousTargetCredential);

            await ShowMessageAsync(
                T("Sources_ConnectionFailed"),
                exception is MediaSourceException sourceException
                    ? FormatSourceError(
                        sourceException.ErrorCode,
                        sourceException.Message)
                    : exception.Message);
        }
    }

    private void RollBackWebDavTarget(
        string sourceId,
        MediaSourceDefinition? previousSource,
        MediaCredentialSnapshot? previousCredential)
    {
        if (previousSource is null)
        {
            _catalog.RemoveSourceItems(sourceId);
            _sources.Remove(sourceId);
            _credentials.RemoveWebDav(sourceId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(
                previousSource.RootLocation) &&
            Uri.TryCreate(
                previousSource.RootLocation,
                UriKind.Absolute,
                out var previousRoot))
        {
            _sources.AddWebDav(
                previousSource.DisplayName,
                previousRoot,
                previousSource.UserName,
                previousSource.CredentialKey);
        }

        RestoreCredential(
            sourceId,
            previousCredential);
    }

    private MediaCredentialSnapshot? ResolveEditorCredential(
        MediaSourceDefinition? existingSource,
        string userName,
        string password)
    {
        if (!string.IsNullOrEmpty(password))
        {
            return new MediaCredentialSnapshot(
                userName,
                password);
        }

        if (string.IsNullOrEmpty(userName))
            return null;

        if (existingSource is not null)
        {
            var previous =
                _credentials.GetWebDav(
                    existingSource);

            if (previous is not null &&
                string.Equals(
                    previous.UserName,
                    userName,
                    StringComparison.Ordinal))
            {
                return previous;
            }
        }

        return new MediaCredentialSnapshot(
            userName,
            string.Empty);
    }

    private async Task BrowseWebDavFoldersAsync(
        TextBox address,
        TextBox userName,
        PasswordBox password,
        MediaSourceDefinition? existingSource,
        TextBlock editorStatus,
        FrameworkElement anchor)
    {
        editorStatus.Visibility = Visibility.Collapsed;

        if (!TryNormalizeWebDavUri(
                address.Text,
                out var rootUri))
        {
            editorStatus.Text =
                T("Sources_InvalidAddress");
            editorStatus.Visibility =
                Visibility.Visible;
            return;
        }

        var normalizedUser =
            userName.Text.Trim();
        var credential =
            ResolveEditorCredential(
                existingSource,
                normalizedUser,
                password.Password);

        var temporarySource =
            new MediaSourceDefinition(
                MediaSourceStore.BuildWebDavSourceId(
                    rootUri,
                    normalizedUser),
                MediaSourceKind.WebDav,
                rootUri.Host,
                rootUri.AbsoluteUri,
                UserName: normalizedUser,
                CredentialKey: credential is null
                    ? null
                    : "inline");

        var provider =
            new WebDavMediaSourceProvider(
                new InlineWebDavCredentialProvider(
                    credential));

        var folderList = new ListView
        {
            IsItemClickEnabled = true,
            SelectionMode = ListViewSelectionMode.None,
            MinHeight = 180,
            MaxHeight = 360
        };

        var currentPathText = new TextBlock
        {
            Text = "/",
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["MetadataText"]
        };

        var statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["MetadataText"]
        };

        var progress = new ProgressRing
        {
            Width = 22,
            Height = 22,
            IsActive = false,
            Visibility = Visibility.Collapsed
        };

        var upButton = new Button
        {
            Content = T("Sources_ParentFolder"),
            MinWidth = 96
        };

        var selectButton = new Button
        {
            Content = T("Sources_SelectCurrentFolder"),
            MinWidth = 132
        };

        var header = new Grid
        {
            ColumnSpacing = 8
        };
        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });
        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        header.Children.Add(upButton);
        Grid.SetColumn(currentPathText, 1);
        header.Children.Add(currentPathText);
        Grid.SetColumn(progress, 2);
        header.Children.Add(progress);

        var footer = new Grid
        {
            ColumnSpacing = 8
        };
        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });
        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        footer.Children.Add(statusText);
        Grid.SetColumn(selectButton, 1);
        footer.Children.Add(selectButton);

        var browserContent = new StackPanel
        {
            Width = 520,
            Spacing = 10
        };
        browserContent.Children.Add(
            new TextBlock
            {
                Text = T("Sources_BrowseFolders"),
                FontSize = 16,
                FontWeight =
                    Windows.UI.Text.FontWeights.SemiBold
            });
        browserContent.Children.Add(header);
        browserContent.Children.Add(folderList);
        browserContent.Children.Add(footer);

        var flyout = new Flyout
        {
            Content = browserContent
        };

        var currentPath = string.Empty;

        async Task LoadFoldersAsync()
        {
            progress.IsActive = true;
            progress.Visibility =
                Visibility.Visible;
            statusText.Text =
                T("Sources_LoadingFolders");
            folderList.ItemsSource = null;

            try
            {
                var folders =
                    new List<WebDavFolderOption>();

                await foreach (var entry in provider.ListAsync(
                                   temporarySource,
                                   currentPath))
                {
                    if (!entry.IsDirectory)
                        continue;

                    folders.Add(
                        new WebDavFolderOption(
                            entry.Name,
                            entry.RelativePath));
                }

                folders.Sort(
                    static (left, right) =>
                        StringComparer.CurrentCultureIgnoreCase
                            .Compare(
                                left.Name,
                                right.Name));

                folderList.ItemsSource = folders;
                statusText.Text = folders.Count == 0
                    ? T("Sources_NoSubfolders")
                    : string.Empty;
                currentPathText.Text =
                    string.IsNullOrEmpty(currentPath)
                        ? "/"
                        : "/" +
                          Uri.UnescapeDataString(
                              currentPath.Trim('/'));
                upButton.IsEnabled =
                    !string.IsNullOrEmpty(
                        currentPath);
            }
            catch (MediaSourceException exception)
            {
                statusText.Text =
                    FormatSourceError(
                        exception.ErrorCode,
                        exception.Message);
            }
            catch (Exception exception)
            {
                statusText.Text =
                    exception.Message;
            }
            finally
            {
                progress.IsActive = false;
                progress.Visibility =
                    Visibility.Collapsed;
            }
        }

        folderList.ItemClick +=
            async (_, args) =>
            {
                if (args.ClickedItem is not
                    WebDavFolderOption folder)
                {
                    return;
                }

                currentPath =
                    folder.RelativePath;
                await LoadFoldersAsync();
            };

        upButton.Click +=
            async (_, _) =>
            {
                currentPath =
                    GetParentWebDavPath(
                        currentPath);
                await LoadFoldersAsync();
            };

        selectButton.Click +=
            (_, _) =>
            {
                address.Text =
                    BuildWebDavFolderUri(
                        rootUri,
                        currentPath)
                    .AbsoluteUri;
                flyout.Hide();
            };

        flyout.ShowAt(anchor);
        await LoadFoldersAsync();
    }

    private static string GetParentWebDavPath(
        string relativePath)
    {
        var trimmed =
            relativePath.Trim('/');

        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        var separator =
            trimmed.LastIndexOf('/');

        return separator < 0
            ? string.Empty
            : trimmed[..(separator + 1)];
    }

    private static Uri BuildWebDavFolderUri(
        Uri rootUri,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(
                relativePath))
        {
            return NormalizeWebDavUri(
                rootUri);
        }

        var escaped =
            string.Join(
                "/",
                relativePath
                    .Trim('/')
                    .Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(
                        Uri.EscapeDataString));

        return new Uri(
            NormalizeWebDavUri(rootUri),
            escaped + "/");
    }

    private static bool TryNormalizeWebDavUri(
        string value,
        out Uri normalized)
    {
        normalized = null!;

        if (!Uri.TryCreate(
                value.Trim(),
                UriKind.Absolute,
                out var rootUri) ||
            (rootUri.Scheme != Uri.UriSchemeHttp &&
             rootUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(rootUri.UserInfo))
        {
            return false;
        }

        normalized =
            NormalizeWebDavUri(rootUri);
        return true;
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

    private sealed record WebDavFolderOption(
        string Name,
        string RelativePath)
    {
        public override string ToString() => Name;
    }

    private sealed class InlineWebDavCredentialProvider(
        MediaCredentialSnapshot? credential)
        : IMediaCredentialProvider
    {
        public MediaCredentialSnapshot? GetWebDav(
            MediaSourceDefinition source) =>
            credential;
    }
}
