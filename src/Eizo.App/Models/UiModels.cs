namespace Eizo.Models;

public enum MediaCategoryKind { Anime, Movies, Series }
public sealed record MediaCardModel(string Title, string NativeTitle, string Meta, double Progress = 0);
public sealed record SourceItemModel(string Id, string Name, string Summary, string Kind, bool Removable = false, bool IsScanning = false, bool CanScan = true, string ScanText = "");
public sealed record EpisodeItemModel(string Number, string Title, string NativeTitle, string Duration, string Status, double Progress = 0);
public sealed record CacheItemModel(string Title, string Source, string Size, string LastAccessed);
