using Eizo.Localization;
using Eizo.MetadataIntegration;
using Eizo.Models;
using Eizo.Recognition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

internal static class MetadataMatchDialog
{
    private static readonly AppLocalizationService Localization =
        AppLocalizationService.Default;

    public static async Task<MediaMetadataMatchCandidate?> ShowAsync(
        XamlRoot xamlRoot,
        CatalogSubjectModel subject)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        ArgumentNullException.ThrowIfNull(subject);

        var recognition = subject.Items
            .Select(static item => item.Recognition)
            .FirstOrDefault(static value =>
                value is
                {
                    Status: MediaRecognitionStatus.Recognized,
                    Title.Length: > 0,
                });

        if (recognition is null)
        {
            await new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = L(
                    "无法手动匹配",
                    "手動照合できません",
                    "Manual match unavailable"),
                Content = L(
                    "这个作品还没有可用于搜索的识别结果。",
                    "検索に利用できる認識結果がありません。",
                    "This title has no recognition result that can be searched."),
                CloseButtonText =
                    L("关闭", "閉じる", "Close"),
            }.ShowAsync();
            return null;
        }

        var providerOptions =
            new List<ProviderOption>
            {
                new(
                    L("全部", "すべて", "All"),
                    null),
                new(
                    "Bangumi",
                    "bangumi"),
            };

        if (MediaScanCoordinator.Default
                .IsTmdbConfigured)
        {
            providerOptions.Add(
                new ProviderOption(
                    "TMDB",
                    "tmdb"));
        }

        var providerBox = new ComboBox
        {
            Header = L(
                "数据源",
                "データソース",
                "Provider"),
            HorizontalAlignment =
                HorizontalAlignment.Stretch,
            ItemsSource = providerOptions,
            DisplayMemberPath =
                nameof(ProviderOption.Label),
            SelectedIndex = 0,
        };

        var queryBox = new TextBox
        {
            Header = L(
                "搜索作品",
                "作品を検索",
                "Search title"),
            Text = subject.Title,
            HorizontalAlignment =
                HorizontalAlignment.Stretch,
        };

        var searchButton = new Button
        {
            Content = L(
                "搜索",
                "検索",
                "Search"),
            HorizontalAlignment =
                HorizontalAlignment.Left,
        };

        var statusText = new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        };

        var results = new ListView
        {
            MinHeight = 220,
            MaxHeight = 360,
            SelectionMode =
                ListViewSelectionMode.Single,
        };

        var matchDialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = L(
                "手动匹配作品",
                "作品を手動で照合",
                "Manual match"),
            PrimaryButtonText = L(
                "使用此匹配",
                "この照合を使用",
                "Use this match"),
            CloseButtonText = L(
                "取消",
                "キャンセル",
                "Cancel"),
            IsPrimaryButtonEnabled = false,
        };

        results.SelectionChanged += (_, _) =>
            matchDialog.IsPrimaryButtonEnabled =
                results.SelectedItem is ListViewItem;

        searchButton.Click += async (_, _) =>
        {
            searchButton.IsEnabled = false;
            matchDialog.IsPrimaryButtonEnabled = false;
            results.Items.Clear();
            statusText.Text =
                L("正在搜索…", "検索中…", "Searching…");

            try
            {
                var provider =
                    (providerBox.SelectedItem as
                        ProviderOption)?.Provider;
                var candidates =
                    await MediaScanCoordinator.Default
                        .SearchMetadataMatchesAsync(
                            recognition,
                            queryBox.Text,
                            provider);

                foreach (var candidate in candidates)
                {
                    var panel = new StackPanel
                    {
                        Spacing = 3,
                    };
                    panel.Children.Add(
                        new TextBlock
                        {
                            Text = candidate.DisplayTitle,
                            FontWeight =
                                Microsoft.UI.Text.FontWeights.SemiBold,
                            TextWrapping = TextWrapping.Wrap,
                        });
                    panel.Children.Add(
                        new TextBlock
                        {
                            Text = candidate.DisplayMeta,
                            Opacity = 0.68,
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                        });

                    results.Items.Add(
                        new ListViewItem
                        {
                            Tag = candidate,
                            Content = panel,
                            HorizontalContentAlignment =
                                HorizontalAlignment.Stretch,
                            Padding =
                                new Thickness(10, 8, 10, 8),
                        });
                }

                statusText.Text =
                    candidates.Count == 0
                        ? L(
                            "没有找到匹配结果，可以更换关键词或数据源。",
                            "一致する結果がありません。検索語またはデータソースを変更してください。",
                            "No matches found. Try another query or provider.")
                        : L(
                            $"找到 {candidates.Count} 个候选。",
                            $"{candidates.Count} 件の候補があります。",
                            $"{candidates.Count} candidates found.");
            }
            catch
            {
                statusText.Text = L(
                    "搜索失败，请稍后重试。",
                    "検索に失敗しました。後でもう一度お試しください。",
                    "Search failed. Try again later.");
            }
            finally
            {
                searchButton.IsEnabled = true;
            }
        };

        var content = new StackPanel
        {
            Spacing = 10,
            MinWidth = 520,
        };
        content.Children.Add(providerBox);
        if (!MediaScanCoordinator.Default
                .IsTmdbConfigured)
        {
            content.Children.Add(
                new TextBlock
                {
                    Text = L(
                        "TMDB 尚未配置，可在设置 → 元数据中添加 Read Access Token。",
                        "TMDB は未設定です。設定 → メタデータで Read Access Token を追加できます。",
                        "TMDB is not configured. Add a Read Access Token in Settings → Metadata."),
                    Opacity = 0.62,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                });
        }
        content.Children.Add(queryBox);
        content.Children.Add(searchButton);
        content.Children.Add(statusText);
        content.Children.Add(results);
        matchDialog.Content = content;

        var result = await matchDialog.ShowAsync();
        return result == ContentDialogResult.Primary &&
               results.SelectedItem is ListViewItem
               {
                   Tag: MediaMetadataMatchCandidate candidate
               }
            ? candidate
            : null;
    }

    private static string L(
        string zhCn,
        string jaJp,
        string enUs) =>
        Localization.CurrentLanguage switch
        {
            "ja-JP" => jaJp,
            "en-US" => enUs,
            _ => zhCn,
        };

    private sealed record ProviderOption(
        string Label,
        string? Provider);
}
