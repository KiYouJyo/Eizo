using Eizo.Bangumi;
using Eizo.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

internal sealed record BangumiSubjectCommentDraft(
    string Comment,
    BangumiCollectionType Type,
    int Rate);

internal static class BangumiCommunityWriteDialogService
{
    public static async Task<BangumiSubjectCommentDraft?>
        PromptSubjectCommentAsync(
            XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        var localization = AppLocalizationService.Default;

        var commentBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120,
            MaxLength = 380,
            PlaceholderText = localization.GetString(
                "Bangumi_WriteCommentPlaceholder"),
        };

        var collectionCombo = new ComboBox
        {
            Header = localization.GetString(
                "Bangumi_WriteCollectionState"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AddCollectionItem(
            collectionCombo,
            localization.GetString("Bangumi_CollectionWish"),
            BangumiCollectionType.Wish);
        AddCollectionItem(
            collectionCombo,
            localization.GetString("Bangumi_CollectionDone"),
            BangumiCollectionType.Done);
        AddCollectionItem(
            collectionCombo,
            localization.GetString("Bangumi_CollectionDoing"),
            BangumiCollectionType.Doing);
        AddCollectionItem(
            collectionCombo,
            localization.GetString("Bangumi_CollectionOnHold"),
            BangumiCollectionType.OnHold);
        AddCollectionItem(
            collectionCombo,
            localization.GetString("Bangumi_CollectionDropped"),
            BangumiCollectionType.Dropped);
        collectionCombo.SelectedIndex = 2;

        var rateBox = new NumberBox
        {
            Header = localization.GetString(
                "Bangumi_WriteRating"),
            Minimum = 0,
            Maximum = 10,
            SpinButtonPlacementMode =
                NumberBoxSpinButtonPlacementMode.Compact,
            Value = 0,
        };

        var warning = new TextBlock
        {
            Text = localization.GetString(
                "Bangumi_WriteCollectionWarning"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        };

        var content = new StackPanel
        {
            Spacing = 10,
            MaxWidth = 560,
        };
        content.Children.Add(commentBox);
        content.Children.Add(collectionCombo);
        content.Children.Add(rateBox);
        content.Children.Add(warning);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = localization.GetString(
                "Bangumi_WriteCommentTitle"),
            Content = content,
            PrimaryButtonText = localization.GetString(
                "Bangumi_Publish"),
            CloseButtonText = localization.GetString(
                "Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() !=
            ContentDialogResult.Primary)
        {
            return null;
        }

        var comment = commentBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(comment) ||
            collectionCombo.SelectedItem is not ComboBoxItem
            {
                Tag: BangumiCollectionType type
            })
        {
            return null;
        }

        var rate = double.IsNaN(rateBox.Value)
            ? 0
            : Math.Clamp(
                (int)Math.Round(rateBox.Value),
                0,
                10);

        return new BangumiSubjectCommentDraft(
            comment,
            type,
            rate);
    }

    public static async Task<string?> PromptReplyAsync(
        XamlRoot xamlRoot,
        string title)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        var localization = AppLocalizationService.Default;

        var textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 140,
            PlaceholderText = localization.GetString(
                "Bangumi_WriteReplyPlaceholder"),
        };

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = textBox,
            PrimaryButtonText = localization.GetString(
                "Bangumi_Publish"),
            CloseButtonText = localization.GetString(
                "Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() !=
            ContentDialogResult.Primary)
        {
            return null;
        }

        var content = textBox.Text.Trim();
        return string.IsNullOrWhiteSpace(content)
            ? null
            : content;
    }

    private static void AddCollectionItem(
        ComboBox comboBox,
        string label,
        BangumiCollectionType type)
    {
        comboBox.Items.Add(
            new ComboBoxItem
            {
                Content = label,
                Tag = type,
            });
    }
}
