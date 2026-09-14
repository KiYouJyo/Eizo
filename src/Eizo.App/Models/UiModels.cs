using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Eizo.Models;

public enum MediaCategoryKind { Anime, Movies, Series }
public sealed record MediaCardModel(
    string Title,
    string NativeTitle,
    string Meta,
    double Progress = 0,
    string? ExternalKey = null);

public sealed record HomeContinueCardModel(
    CatalogMediaItemModel MediaItem,
    string Title,
    string NativeTitle,
    string Meta,
    double Progress,
    string? ArtworkUrl);


public sealed class SourceItemModel : INotifyPropertyChanged
{
    private string _name;
    private string _summary;
    private string _kind;
    private bool _removable;
    private bool _isScanning;
    private bool _isScraping;
    private bool _canScan;
    private bool _canScrape;
    private string _scanText;
    private string _scrapeText;
    private double _scanProgressSize;
    private double _scanSpacing;
    private double _scrapeProgressSize;
    private double _scrapeSpacing;

    public SourceItemModel(
        string id,
        string name,
        string summary,
        string kind,
        bool Removable = false,
        bool IsScanning = false,
        bool IsScraping = false,
        bool CanScan = true,
        bool CanScrape = false,
        string ScanText = "",
        string ScrapeText = "",
        double ScanProgressSize = 0,
        double ScanSpacing = 0,
        double ScrapeProgressSize = 0,
        double ScrapeSpacing = 0)
    {
        Id = id;
        _name = name;
        _summary = summary;
        _kind = kind;
        _removable = Removable;
        _isScanning = IsScanning;
        _isScraping = IsScraping;
        _canScan = CanScan;
        _canScrape = CanScrape;
        _scanText = ScanText;
        _scrapeText = ScrapeText;
        _scanProgressSize = ScanProgressSize;
        _scanSpacing = ScanSpacing;
        _scrapeProgressSize = ScrapeProgressSize;
        _scrapeSpacing = ScrapeSpacing;
    }

    public string Id { get; }
    public string Name { get => _name; private set => Set(ref _name, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public string Kind { get => _kind; private set => Set(ref _kind, value); }
    public bool Removable { get => _removable; private set => Set(ref _removable, value); }
    public bool IsScanning { get => _isScanning; private set => Set(ref _isScanning, value); }
    public bool IsScraping { get => _isScraping; private set => Set(ref _isScraping, value); }
    public bool CanScan { get => _canScan; private set => Set(ref _canScan, value); }
    public bool CanScrape { get => _canScrape; private set => Set(ref _canScrape, value); }
    public string ScanText { get => _scanText; private set => Set(ref _scanText, value); }
    public string ScrapeText { get => _scrapeText; private set => Set(ref _scrapeText, value); }
    public double ScanProgressSize { get => _scanProgressSize; private set => Set(ref _scanProgressSize, value); }
    public double ScanSpacing { get => _scanSpacing; private set => Set(ref _scanSpacing, value); }
    public double ScrapeProgressSize { get => _scrapeProgressSize; private set => Set(ref _scrapeProgressSize, value); }
    public double ScrapeSpacing { get => _scrapeSpacing; private set => Set(ref _scrapeSpacing, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateFrom(SourceItemModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!string.Equals(Id, value.Id, StringComparison.Ordinal))
            throw new ArgumentException("Source IDs must match.", nameof(value));

        Name = value.Name;
        Summary = value.Summary;
        Kind = value.Kind;
        Removable = value.Removable;
        IsScanning = value.IsScanning;
        IsScraping = value.IsScraping;
        CanScan = value.CanScan;
        CanScrape = value.CanScrape;
        ScanText = value.ScanText;
        ScrapeText = value.ScrapeText;
        ScanProgressSize = value.ScanProgressSize;
        ScanSpacing = value.ScanSpacing;
        ScrapeProgressSize = value.ScrapeProgressSize;
        ScrapeSpacing = value.ScrapeSpacing;
    }

    private void Set<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record EpisodeItemModel(
    string Number,
    string Title,
    string NativeTitle,
    string Duration,
    string Status,
    double Progress = 0,
    CatalogMediaItemModel? MediaItem = null);
public sealed record CachedVideoPlaybackRequest(
    string GroupKey,
    string Title,
    string Source,
    long SizeBytes);

public sealed class CacheItemModel : INotifyPropertyChanged
{
    private string _title;
    private string _source;
    private string _size;
    private string _lastAccessed;
    private double _progressPercent;
    private string _progressText;
    private string _status;
    private bool _isCompleted;
    private bool _isPaused;
    private bool _isFailed;
    private string? _groupKey;
    private string? _taskKey;
    private CachedVideoPlaybackRequest? _playbackRequest;
    private string _deleteText;
    private string _speedText;
    private bool _isDownloading;

    public CacheItemModel(
        string id,
        string title,
        string source,
        string size,
        string lastAccessed,
        double progressPercent = 100,
        string progressText = "",
        string status = "",
        bool isCompleted = false,
        string? groupKey = null,
        string? taskKey = null,
        CachedVideoPlaybackRequest? playbackRequest = null,
        string deleteText = "",
        bool isPaused = false,
        bool isFailed = false,
        string speedText = "",
        bool isDownloading = false)
    {
        Id = id;
        _title = title;
        _source = source;
        _size = size;
        _lastAccessed = lastAccessed;
        _progressPercent = progressPercent;
        _progressText = progressText;
        _status = status;
        _isCompleted = isCompleted;
        _groupKey = groupKey;
        _taskKey = taskKey;
        _playbackRequest = playbackRequest;
        _deleteText = deleteText;
        _isPaused = isPaused;
        _isFailed = isFailed;
        _speedText = speedText;
        _isDownloading = isDownloading;
    }

    public string Id { get; }
    public string Title { get => _title; private set => Set(ref _title, value); }
    public string Source { get => _source; private set => Set(ref _source, value); }
    public string Size { get => _size; private set => Set(ref _size, value); }
    public string LastAccessed { get => _lastAccessed; private set => Set(ref _lastAccessed, value); }
    public double ProgressPercent { get => _progressPercent; private set => Set(ref _progressPercent, value); }
    public string ProgressText { get => _progressText; private set => Set(ref _progressText, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool IsCompleted { get => _isCompleted; private set => Set(ref _isCompleted, value); }
    public bool IsPaused { get => _isPaused; private set => Set(ref _isPaused, value); }
    public bool IsFailed { get => _isFailed; private set => Set(ref _isFailed, value); }
    public string? GroupKey { get => _groupKey; private set => Set(ref _groupKey, value); }
    public string? TaskKey { get => _taskKey; private set => Set(ref _taskKey, value); }
    public CachedVideoPlaybackRequest? PlaybackRequest { get => _playbackRequest; private set => Set(ref _playbackRequest, value); }
    public string DeleteText { get => _deleteText; private set => Set(ref _deleteText, value); }
    public string SpeedText { get => _speedText; private set => Set(ref _speedText, value); }
    public bool IsDownloading { get => _isDownloading; private set => Set(ref _isDownloading, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateFrom(CacheItemModel value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!string.Equals(
                Id,
                value.Id,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Cache item IDs must match.",
                nameof(value));
        }

        Title = value.Title;
        Source = value.Source;
        Size = value.Size;
        LastAccessed = value.LastAccessed;
        ProgressPercent = value.ProgressPercent;
        ProgressText = value.ProgressText;
        Status = value.Status;
        IsCompleted = value.IsCompleted;
        IsPaused = value.IsPaused;
        IsFailed = value.IsFailed;
        GroupKey = value.GroupKey;
        TaskKey = value.TaskKey;
        PlaybackRequest = value.PlaybackRequest;
        DeleteText = value.DeleteText;
        SpeedText = value.SpeedText;
        IsDownloading = value.IsDownloading;
    }

    private void Set<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
