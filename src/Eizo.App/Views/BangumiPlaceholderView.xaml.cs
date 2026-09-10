using Eizo.Localization;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

internal enum BangumiPlaceholderKind
{
    Calendar,
    Seasonal,
    Discover,
    Following
}

public sealed partial class BangumiPlaceholderView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly BangumiPlaceholderKind _kind;

    internal BangumiPlaceholderView(BangumiPlaceholderKind kind)
    {
        _kind = kind;
        InitializeComponent();
        ApplyText();
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        var descriptor = _kind switch
        {
            BangumiPlaceholderKind.Calendar => (
                "Nav_BroadcastCalendar",
                "Bangumi_CalendarPlaceholderDescription",
                "\uE787"),
            BangumiPlaceholderKind.Seasonal => (
                "Nav_SeasonalAnime",
                "Bangumi_SeasonalPlaceholderDescription",
                "\uE8B2"),
            BangumiPlaceholderKind.Discover => (
                "Nav_RankDiscover",
                "Bangumi_DiscoverPlaceholderDescription",
                "\uE721"),
            _ => (
                "Nav_MyFollowing",
                "Bangumi_FollowingPlaceholderDescription",
                "\uE77B")
        };

        PageTitle.Text = T(descriptor.Item1);
        PageSubtitle.Text = T("Bangumi_PlaceholderSubtitle");
        StatusText.Text = T("Bangumi_PlaceholderStatus");
        DescriptionText.Text = T(descriptor.Item2);
        PlaceholderIcon.Glyph = descriptor.Item3;
    }
}
