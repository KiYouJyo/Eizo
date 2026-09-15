using Microsoft.UI.Xaml.Media;

namespace Eizo.Models;

public sealed record EpisodeDisplayItemModel(
    string Number,
    string Title,
    string NativeTitle,
    string Duration,
    string Status,
    double Progress = 0,
    CatalogMediaItemModel? MediaItem = null,
    ImageSource? Thumbnail = null)
{
    public string AirDate { get; init; } = string.Empty;

    public EpisodeDisplayItemModel Self => this;

    public string SecondaryText =>
        string.Join(
            " · ",
            new[]
            {
                NativeTitle,
                AirDate,
            }.Where(static value =>
                !string.IsNullOrWhiteSpace(value)));

    public string PlaybackText =>
        string.IsNullOrWhiteSpace(Duration)
            ? Status
            : $"{Status} / {Duration}";

    public bool CanCache =>
        MediaItem?.Location?.Kind ==
        MediaLocationKind.RemoteUri;
}
