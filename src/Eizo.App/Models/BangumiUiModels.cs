using Eizo.Bangumi;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eizo.Models;

public sealed record BangumiCardViewModel(
    BangumiSubjectCard Subject,
    BitmapImage? Artwork,
    string Title,
    string Subtitle,
    string MetaLine,
    string SourceLabel,
    string TypeLabel);

public sealed record BangumiDayOption(
    int WeekdayId,
    string Label);
