using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Eizo.Views;

internal sealed class WebDavFolderPickerWindow
{
    private const int GwlpHwndParent = -8;

    private readonly Window _window = new();
    private readonly IMediaSourceProvider _provider;
    private readonly MediaSourceDefinition _source;
    private readonly Uri _rootUri;
    private readonly Func<string?, string?, string> _formatError;
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly HashSet<string> _selectedPaths;
    private readonly TaskCompletionSource<IReadOnlyList<string>?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly BreadcrumbBar _breadcrumb = new();
    private readonly ListView _folderList = new();
    private readonly CheckBox _currentFolderCheckBox = new();
    private readonly TextBlock _selectionSummary = new();
    private readonly TextBlock _statusText = new();
    private readonly ProgressRing _progress = new();
    private readonly Button _confirmButton = new();

    private string _currentPath = string.Empty;
    private bool _isLoading;
    private readonly ElementTheme _windowTheme;
    private readonly Windows.UI.Color _surfaceColor;

    public WebDavFolderPickerWindow(
        IMediaSourceProvider provider,
        MediaSourceDefinition source,
        Uri rootUri,
        IEnumerable<string>? selectedPaths,
        Func<string?, string?, string> formatError)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _rootUri = rootUri ?? throw new ArgumentNullException(nameof(rootUri));
        _formatError = formatError ?? throw new ArgumentNullException(nameof(formatError));
        _selectedPaths = new HashSet<string>(
            selectedPaths ?? [],
            StringComparer.OrdinalIgnoreCase);

        _windowTheme = ResolveWindowTheme();
        _surfaceColor = _windowTheme == ElementTheme.Dark
            ? ColorHelper.FromArgb(255, 26, 35, 35)
            : ColorHelper.FromArgb(255, 229, 249, 249);

        ConfigureWindow();
        _window.Content = BuildContent();
        _window.Closed += (_, _) =>
            _completion.TrySetResult(null);
    }

    private string T(string key) => _localization.GetString(key);

    public async Task<IReadOnlyList<string>?> ShowAsync(
        IntPtr ownerWindowHandle)
    {
        _window.Activate();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        if (hwnd != IntPtr.Zero &&
            ownerWindowHandle != IntPtr.Zero)
        {
            _ = SetWindowLongPtr(
                hwnd,
                GwlpHwndParent,
                ownerWindowHandle);
        }

        try
        {
            const int width = 1040;
            const int height = 760;

            if (ownerWindowHandle != IntPtr.Zero &&
                GetWindowRect(
                    ownerWindowHandle,
                    out var ownerBounds))
            {
                var ownerWidth =
                    ownerBounds.Right -
                    ownerBounds.Left;
                var ownerHeight =
                    ownerBounds.Bottom -
                    ownerBounds.Top;

                var x = ownerBounds.Left +
                    Math.Max(
                        0,
                        (ownerWidth - width) / 2);
                var y = ownerBounds.Top +
                    Math.Max(
                        0,
                        (ownerHeight - height) / 2);

                _window.AppWindow.MoveAndResize(
                    new RectInt32(
                        x,
                        y,
                        width,
                        height));
            }
            else
            {
                _window.AppWindow.Resize(
                    new SizeInt32(
                        width,
                        height));
            }
        }
        catch
        {
        }

        await LoadFoldersAsync();
        return await _completion.Task;
    }

    private void ConfigureWindow()
    {
        _window.Title = T("Sources_EditReadFolders");

        try
        {
            _window.SystemBackdrop =
                new MicaBackdrop();

            var titleBar =
                _window.AppWindow.TitleBar;
            var foreground =
                _windowTheme == ElementTheme.Dark
                    ? Colors.White
                    : Colors.Black;
            var secondaryForeground =
                _windowTheme == ElementTheme.Dark
                    ? ColorHelper.FromArgb(
                        255,
                        210,
                        218,
                        218)
                    : ColorHelper.FromArgb(
                        255,
                        32,
                        32,
                        32);
            var hover =
                _windowTheme == ElementTheme.Dark
                    ? ColorHelper.FromArgb(
                        255,
                        38,
                        50,
                        50)
                    : ColorHelper.FromArgb(
                        255,
                        214,
                        235,
                        235);
            var pressed =
                _windowTheme == ElementTheme.Dark
                    ? ColorHelper.FromArgb(
                        255,
                        52,
                        66,
                        66)
                    : ColorHelper.FromArgb(
                        255,
                        198,
                        222,
                        222);

            titleBar.BackgroundColor =
                _surfaceColor;
            titleBar.InactiveBackgroundColor =
                _surfaceColor;
            titleBar.ForegroundColor =
                foreground;
            titleBar.InactiveForegroundColor =
                secondaryForeground;
            titleBar.ButtonBackgroundColor =
                _surfaceColor;
            titleBar.ButtonInactiveBackgroundColor =
                _surfaceColor;
            titleBar.ButtonForegroundColor =
                foreground;
            titleBar.ButtonInactiveForegroundColor =
                secondaryForeground;
            titleBar.ButtonHoverBackgroundColor =
                hover;
            titleBar.ButtonHoverForegroundColor =
                foreground;
            titleBar.ButtonPressedBackgroundColor =
                pressed;
            titleBar.ButtonPressedForegroundColor =
                foreground;
        }
        catch
        {
        }
    }

    private static ElementTheme ResolveWindowTheme()
    {
        var persisted =
            ThemePreferenceStore.Load();

        if (persisted != ElementTheme.Default)
            return persisted;

        try
        {
            var background =
                new Windows.UI.ViewManagement.UISettings()
                    .GetColorValue(
                        Windows.UI.ViewManagement.UIColorType.Background);

            var luminance =
                0.2126 * background.R +
                0.7152 * background.G +
                0.0722 * background.B;

            return luminance < 128
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }
        catch
        {
            return ElementTheme.Light;
        }
    }

    private FrameworkElement BuildContent()
    {
        var root = new Grid
        {
            Padding = new Thickness(24),
            RowSpacing = 14,
            RequestedTheme = _windowTheme,
            Background =
                new SolidColorBrush(
                    _surfaceColor)
        };

        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(
                    1,
                    GridUnitType.Star)
            });
        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel
        {
            Spacing = 4
        };
        heading.Children.Add(
            new TextBlock
            {
                Text = T("Sources_EditReadFolders"),
                FontSize = 24
            });
        heading.Children.Add(
            new TextBlock
            {
                Text = T("Sources_FolderPickerHint"),
                TextWrapping = TextWrapping.Wrap,
                Foreground =
                    (Brush)Application.Current.Resources[
                        "TextFillColorSecondaryBrush"]
            });
        root.Children.Add(heading);

        var pathPanel = new StackPanel
        {
            Spacing = 8
        };

        _breadcrumb.HorizontalAlignment =
            HorizontalAlignment.Stretch;
        _breadcrumb.ItemClicked +=
            async (_, args) =>
            {
                if (args.Item is not
                    WebDavBreadcrumbItem item ||
                    _isLoading)
                {
                    return;
                }

                _currentPath = item.RelativePath;
                await LoadFoldersAsync();
            };

        var currentFolderRow = new Grid
        {
            ColumnSpacing = 10
        };
        currentFolderRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        currentFolderRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });
        currentFolderRow.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        _currentFolderCheckBox.VerticalAlignment =
            VerticalAlignment.Center;
        _currentFolderCheckBox.Checked +=
            (_, _) =>
            {
                if (_isLoading)
                    return;

                _selectedPaths.Add(_currentPath);
                UpdateSelectionSummary();
            };
        _currentFolderCheckBox.Unchecked +=
            (_, _) =>
            {
                if (_isLoading)
                    return;

                _selectedPaths.Remove(_currentPath);
                UpdateSelectionSummary();
            };
        currentFolderRow.Children.Add(
            _currentFolderCheckBox);

        var currentFolderLabel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment =
                VerticalAlignment.Center
        };
        currentFolderLabel.Children.Add(
            new FontIcon
            {
                Glyph = "\uE8B7",
                FontSize = 16
            });
        currentFolderLabel.Children.Add(
            new TextBlock
            {
                Text = T("Sources_SelectCurrentFolder"),
                TextTrimming =
                    TextTrimming.CharacterEllipsis,
                VerticalAlignment =
                    VerticalAlignment.Center
            });
        Grid.SetColumn(
            currentFolderLabel,
            1);
        currentFolderRow.Children.Add(
            currentFolderLabel);

        _progress.Width = 20;
        _progress.Height = 20;
        _progress.Visibility = Visibility.Collapsed;
        Grid.SetColumn(_progress, 2);
        currentFolderRow.Children.Add(_progress);

        pathPanel.Children.Add(_breadcrumb);
        pathPanel.Children.Add(currentFolderRow);
        Grid.SetRow(pathPanel, 1);
        root.Children.Add(pathPanel);

        _folderList.SelectionMode =
            ListViewSelectionMode.None;
        _folderList.HorizontalAlignment =
            HorizontalAlignment.Stretch;
        _folderList.HorizontalContentAlignment =
            HorizontalAlignment.Stretch;

        ScrollViewer.SetHorizontalScrollMode(
            _folderList,
            ScrollMode.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(
            _folderList,
            ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollMode(
            _folderList,
            ScrollMode.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(
            _folderList,
            ScrollBarVisibility.Auto);

        var itemStyle = new Style(
            typeof(ListViewItem));
        itemStyle.Setters.Add(
            new Setter(
                Control.HorizontalContentAlignmentProperty,
                HorizontalAlignment.Stretch));
        itemStyle.Setters.Add(
            new Setter(
                Control.PaddingProperty,
                new Thickness(6, 2, 6, 2)));
        _folderList.ItemContainerStyle =
            itemStyle;

        Grid.SetRow(_folderList, 2);
        root.Children.Add(_folderList);

        var footer = new Grid
        {
            ColumnSpacing = 12
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

        var footerText = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment =
                VerticalAlignment.Center
        };
        _selectionSummary.Foreground =
            (Brush)Application.Current.Resources[
                "TextFillColorSecondaryBrush"];
        _statusText.Foreground =
            (Brush)Application.Current.Resources[
                "TextFillColorSecondaryBrush"];
        _statusText.TextWrapping =
            TextWrapping.Wrap;
        footerText.Children.Add(
            _selectionSummary);
        footerText.Children.Add(
            _statusText);
        footer.Children.Add(footerText);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var cancelButton = new Button
        {
            Content = T("Common_Cancel"),
            MinWidth = 92
        };
        cancelButton.Click +=
            (_, _) =>
            {
                _completion.TrySetResult(null);
                _window.Close();
            };
        actions.Children.Add(cancelButton);

        _confirmButton.Content =
            T("Common_Confirm");
        _confirmButton.MinWidth = 92;
        _confirmButton.Style =
            Application.Current.Resources[
                "AccentButtonStyle"] as Style;
        _confirmButton.Click +=
            (_, _) =>
            {
                var result = _selectedPaths
                    .OrderBy(
                        static path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                _completion.TrySetResult(result);
                _window.Close();
            };
        actions.Children.Add(
            _confirmButton);

        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        UpdateSelectionSummary();
        return root;
    }

    private async Task LoadFoldersAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        _folderList.IsEnabled = false;
        _breadcrumb.IsEnabled = false;
        _currentFolderCheckBox.IsEnabled = false;
        _confirmButton.IsEnabled = false;
        _progress.IsActive = true;
        _progress.Visibility = Visibility.Visible;
        _statusText.Text =
            T("Sources_LoadingFolders");

        try
        {
            var folders =
                new List<WebDavFolderOption>();

            await foreach (var entry in _provider.ListAsync(
                               _source,
                               _currentPath))
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

            _breadcrumb.ItemsSource =
                BuildBreadcrumbItems(
                    _currentPath);
            _folderList.Items.Clear();

            foreach (var folder in folders)
            {
                _folderList.Items.Add(
                    CreateFolderRow(folder));
            }

            _currentFolderCheckBox.IsChecked =
                _selectedPaths.Contains(
                    _currentPath);

            _statusText.Text =
                folders.Count == 0
                    ? T("Sources_NoSubfolders")
                    : string.Empty;
        }
        catch (MediaSourceException exception)
        {
            _statusText.Text =
                _formatError(
                    exception.ErrorCode,
                    exception.Message);
        }
        catch (Exception exception)
        {
            _statusText.Text =
                exception.Message;
        }
        finally
        {
            _progress.IsActive = false;
            _progress.Visibility = Visibility.Collapsed;
            _folderList.IsEnabled = true;
            _breadcrumb.IsEnabled = true;
            _currentFolderCheckBox.IsEnabled = true;
            _confirmButton.IsEnabled = true;
            _isLoading = false;
            UpdateSelectionSummary();
        }
    }

    private FrameworkElement CreateFolderRow(
        WebDavFolderOption folder)
    {
        var row = new Grid
        {
            MinHeight = 44,
            ColumnSpacing = 10,
            Tag = folder
        };

        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });
        row.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        var checkBox = new CheckBox
        {
            IsChecked =
                _selectedPaths.Contains(
                    folder.RelativePath),
            VerticalAlignment =
                VerticalAlignment.Center,
            Tag = folder.RelativePath
        };
        checkBox.Checked +=
            (_, _) =>
            {
                if (checkBox.Tag is string path)
                {
                    _selectedPaths.Add(path);
                    UpdateSelectionSummary();
                }
            };
        checkBox.Unchecked +=
            (_, _) =>
            {
                if (checkBox.Tag is string path)
                {
                    _selectedPaths.Remove(path);
                    UpdateSelectionSummary();
                }
            };
        row.Children.Add(checkBox);

        var label = new Grid
        {
            ColumnSpacing = 8
        };
        label.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        label.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        label.Children.Add(
            new FontIcon
            {
                Glyph = "\uE8B7",
                FontSize = 16,
                VerticalAlignment =
                    VerticalAlignment.Center
            });

        var name = new TextBlock
        {
            Text = folder.Name,
            TextTrimming =
                TextTrimming.CharacterEllipsis,
            VerticalAlignment =
                VerticalAlignment.Center
        };
        Grid.SetColumn(name, 1);
        label.Children.Add(name);
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        var enterButton = new Button
        {
            Width = 36,
            Height = 32,
            MinWidth = 36,
            Padding = new Thickness(0),
            Tag = folder.RelativePath
        };
        enterButton.Content =
            new FontIcon
            {
                Glyph = "\uE76C",
                FontSize = 12
            };
        ToolTipService.SetToolTip(
            enterButton,
            T("Sources_OpenFolder"));
        enterButton.Click +=
            async (_, _) =>
            {
                if (enterButton.Tag is not
                    string path ||
                    _isLoading)
                {
                    return;
                }

                _currentPath = path;
                await LoadFoldersAsync();
            };
        Grid.SetColumn(enterButton, 2);
        row.Children.Add(enterButton);

        label.DoubleTapped +=
            async (_, args) =>
            {
                if (_isLoading)
                    return;

                args.Handled = true;
                _currentPath =
                    folder.RelativePath;
                await LoadFoldersAsync();
            };

        return row;
    }

    private IReadOnlyList<WebDavBreadcrumbItem>
        BuildBreadcrumbItems(
            string relativePath)
    {
        var items =
            new List<WebDavBreadcrumbItem>
            {
                new(
                    _rootUri.Host,
                    string.Empty)
            };

        var segments = relativePath
            .Trim('/')
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        var current = string.Empty;

        foreach (var segment in segments)
        {
            current +=
                Uri.UnescapeDataString(
                    segment) + "/";

            items.Add(
                new WebDavBreadcrumbItem(
                    Uri.UnescapeDataString(
                        segment),
                    current));
        }

        return items;
    }

    private void UpdateSelectionSummary()
    {
        _selectionSummary.Text =
            _selectedPaths.Count == 0
                ? T("Sources_AllFolders")
                : string.Format(
                    T("Sources_SelectedFoldersFormat"),
                    _selectedPaths.Count);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(
        IntPtr hWnd,
        out NativeRect rect);

    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport(
        "user32.dll",
        EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(
        IntPtr hWnd,
        int nIndex,
        IntPtr newLong);

    private sealed record WebDavFolderOption(
        string Name,
        string RelativePath);

    private sealed record WebDavBreadcrumbItem(
        string Name,
        string RelativePath)
    {
        public override string ToString() =>
            Name;
    }
}
