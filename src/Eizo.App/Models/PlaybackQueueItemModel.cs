using System.ComponentModel;
using System.Runtime.CompilerServices;
using Eizo.Playback;

namespace Eizo.Models;

public sealed class PlaybackQueueItemModel : INotifyPropertyChanged
{
    private string _status;

    public PlaybackQueueItemModel(
        int index,
        string number,
        string title,
        string secondaryTitle,
        string sourceLabel,
        PlaybackSource source,
        CatalogMediaItemModel? catalogItem = null,
        string status = "")
    {
        Index = index;
        Number = number;
        Title = title;
        SecondaryTitle = secondaryTitle;
        SourceLabel = sourceLabel;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        CatalogItem = catalogItem;
        _status = status;
    }

    public int Index { get; }

    public string Number { get; }

    public string Title { get; }

    public string SecondaryTitle { get; }

    public string SourceLabel { get; }

    public PlaybackSource Source { get; }

    public CatalogMediaItemModel? CatalogItem { get; }

    public string Status
    {
        get => _status;
        internal set
        {
            if (string.Equals(_status, value, StringComparison.Ordinal))
                return;

            _status = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
