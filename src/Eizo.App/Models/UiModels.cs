using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Eizo.Models;

public enum MediaCategoryKind { Anime, Movies, Series }
public sealed record MediaCardModel(string Title, string NativeTitle, string Meta, double Progress = 0);

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
    CatalogMediaItemModel? MediaItem = null,
    ImageSource? Thumbnail = null);
public sealed record CacheItemModel(string Title, string Source, string Size, string LastAccessed);
