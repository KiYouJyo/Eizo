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
    ImageSource? Thumbnail = null);
