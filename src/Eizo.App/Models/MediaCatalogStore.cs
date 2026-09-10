using System.Text.Json;
using Eizo.Metadata.Recognition;

namespace Eizo.Models;

public sealed class MediaCatalogStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = true };

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "catalog.json");

    private static readonly HashSet<string> SupportedVideoExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".mp4", ".m4v", ".mov", ".avi", ".webm",
            ".ts", ".m2ts", ".wmv", ".mpg", ".mpeg"
        };

    private static readonly RecognitionEngine Recognizer = new();

    private readonly object _sync = new();
    private readonly List<CatalogMediaItemModel> _items;

    private MediaCatalogStore()
    {
        _items = LoadCore();
        if (PruneMissingLocalFilesCore())
            SaveCore(_items);
    }

    public static MediaCatalogStore Default { get; } = new();

    public event EventHandler? Changed;

    public IReadOnlyList<CatalogMediaItemModel> Snapshot()
    {
        lock (_sync)
            return _items
                .Where(IsAvailable)
                .ToArray();
    }

    public IReadOnlyList<CatalogMediaItemModel> SnapshotForSource(string sourceId)
    {
        lock (_sync)
        {
            return _items
                .Where(IsAvailable)
                .Where(item =>
                    string.Equals(
                        item.Location?.SourceId,
                        sourceId,
                        StringComparison.Ordinal))
                .ToArray();
        }
    }

    public bool RegisterLocalFile(string path)
    {
        if (!IsSupportedVideoPath(path) || !File.Exists(path))
            return false;

        var fullPath = Path.GetFullPath(path);
        var fileInfo = new FileInfo(fullPath);
        var sourceId = MediaSourceStore.Default.ResolveLocalSourceId(fullPath);

        var item = CreateLocalItem(
            sourceId,
            fileInfo,
            fileInfo.Name);
        var changed = false;

        lock (_sync)
        {
            var index = _items.FindIndex(existing =>
                existing.Location is
                {
                    Kind: MediaLocationKind.LocalFile
                } location &&
                string.Equals(
                    Path.GetFullPath(location.Locator),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                if (_items[index] != item)
                {
                    _items[index] = item;
                    changed = true;
                }
            }
            else
            {
                _items.Add(item);
                changed = true;
            }

            if (changed)
                SaveCore(_items);
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    public async Task<int> ScanSourceAsync(
        MediaSourceDefinition source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Kind == MediaSourceKind.Local)
        {
            return await Task.Run(
                () => ScanLocalSource(source),
                cancellationToken);
        }

        if (!MediaSourceProviderRegistry.TryGet(
                source.Kind,
                out var provider))
        {
            throw new MediaSourceException(
                "ProviderUnavailable",
                $"No provider is registered for {source.Kind}.");
        }

        var discoveredEntries = new List<MediaSourceEntry>();
        var pending = new Queue<string>();
        var visited = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var scanRoots = source.SelectedPaths is { Count: > 0 }
            ? source.SelectedPaths
            : [string.Empty];

        foreach (var scanRoot in scanRoots)
            pending.Enqueue(scanRoot);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = pending.Dequeue();
            if (!visited.Add(relativePath))
                continue;

            await foreach (var entry in provider.ListAsync(
                               source,
                               relativePath,
                               cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsDirectory)
                {
                    if (visited.Count + pending.Count < 100_000)
                        pending.Enqueue(entry.RelativePath);

                    continue;
                }

                var locator = entry.Locator;
                if (string.IsNullOrWhiteSpace(locator) ||
                    !IsSupportedVideoPath(locator))
                {
                    continue;
                }

                discoveredEntries.Add(entry);
            }
        }

        var discovered = await Task.Run(
            () => CreateRemoteItems(
                source.Id,
                discoveredEntries,
                cancellationToken),
            cancellationToken);

        lock (_sync)
        {
            _items.RemoveAll(item =>
                string.Equals(
                    item.Location?.SourceId,
                    source.Id,
                    StringComparison.Ordinal));

            _items.AddRange(discovered);
            SaveCore(_items);
        }

        MediaSourceStore.Default.MarkScanned(
            source.Id,
            DateTimeOffset.UtcNow);

        Changed?.Invoke(this, EventArgs.Empty);
        return discovered.Count;
    }

    public int ScanLocalSource(MediaSourceDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Kind != MediaSourceKind.Local ||
            string.IsNullOrWhiteSpace(source.RootLocation) ||
            !Directory.Exists(source.RootLocation))
        {
            return 0;
        }

        var root = Path.GetFullPath(source.RootLocation);

        var discovered = EnumerateVideoFilesSafe(root)
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists)
            .Select(info => CreateLocalItem(
                source.Id,
                info,
                Path.GetRelativePath(root, info.FullName)))
            .ToArray();

        var discoveredPaths = discovered
            .Select(item => item.LocalPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (_sync)
        {
            _items.RemoveAll(item =>
                string.Equals(
                    item.Location?.SourceId,
                    source.Id,
                    StringComparison.Ordinal) ||
                (item.LocalPath is { Length: > 0 } localPath &&
                 discoveredPaths.Contains(Path.GetFullPath(localPath))));

            _items.AddRange(discovered);
            SaveCore(_items);
        }

        MediaSourceStore.Default.MarkScanned(
            source.Id,
            DateTimeOffset.UtcNow);

        Changed?.Invoke(this, EventArgs.Empty);
        return discovered.Length;
    }

    public void RemoveSourceItems(string sourceId)
    {
        bool changed;

        lock (_sync)
        {
            changed = _items.RemoveAll(item =>
                string.Equals(
                    item.Location?.SourceId,
                    sourceId,
                    StringComparison.Ordinal)) > 0;

            if (changed)
                SaveCore(_items);
        }

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public static bool IsSupportedVideoPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var candidate = path;

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
            !uri.IsFile)
        {
            candidate = Uri.UnescapeDataString(uri.AbsolutePath);
        }

        return SupportedVideoExtensions.Contains(
            Path.GetExtension(candidate));
    }

    private static List<CatalogMediaItemModel> CreateRemoteItems(
        string sourceId,
        IReadOnlyList<MediaSourceEntry> entries,
        CancellationToken cancellationToken)
    {
        var result = new List<CatalogMediaItemModel>(entries.Count);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(CreateRemoteItem(sourceId, entry));
        }

        return result;
    }

    private static CatalogMediaItemModel CreateRemoteItem(
        string sourceId,
        MediaSourceEntry entry)
    {
        var title = Path.GetFileNameWithoutExtension(
            entry.Name);

        if (string.IsNullOrWhiteSpace(title))
            title = entry.Name;

        var logicalPath = string.IsNullOrWhiteSpace(entry.RelativePath)
            ? entry.Name
            : entry.RelativePath;
        var recognition = TryRecognize(logicalPath);

        return new CatalogMediaItemModel(
            title,
            ParsedTitle: recognition?.Title,
            NativeTitle: null,
            Category: null,
            Meta: string.Empty,
            Location: new MediaLocationModel(
                sourceId,
                MediaLocationKind.RemoteUri,
                entry.Locator!,
                entry.SizeBytes,
                entry.ModifiedUtc),
            Recognition: recognition);
    }

    private static CatalogMediaItemModel CreateLocalItem(
        string sourceId,
        FileInfo fileInfo,
        string logicalPath)
    {
        var recognition = TryRecognize(logicalPath);

        return new CatalogMediaItemModel(
            Path.GetFileNameWithoutExtension(fileInfo.Name),
            ParsedTitle: recognition?.Title,
            NativeTitle: null,
            Category: null,
            Meta: string.Empty,
            Location: new MediaLocationModel(
                sourceId,
                MediaLocationKind.LocalFile,
                fileInfo.FullName,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc),
            Recognition: recognition);
    }

    private static CatalogRecognitionModel? TryRecognize(string logicalPath)
    {
        if (string.IsNullOrWhiteSpace(logicalPath))
            return null;

        try
        {
            var result = Recognizer.Recognize(
                new RecognitionRequest(logicalPath));

            if (result.Title is null &&
                result.MediaKind ==
                    Eizo.Metadata.Recognition.MediaKind.Unknown)
            {
                return null;
            }

            return new CatalogRecognitionModel(
                MapMediaKind(result.MediaKind),
                MapSpecialKind(result.SpecialKind),
                MapEpisodePart(result.EpisodePart),
                result.IsFinalEpisode,
                result.Title,
                result.TitleCandidates
                    .Select(candidate =>
                        new CatalogRecognitionTitleCandidateModel(
                            candidate.Title,
                            candidate.Confidence,
                            candidate.Source,
                            candidate.IsPrimary))
                    .ToList(),
                result.SeasonNumber,
                result.CourNumber,
                result.EpisodeNumber,
                result.EpisodeEndNumber,
                result.SpecialNumber,
                result.Year,
                result.Confidence,
                MapConfidenceLevel(result.ConfidenceLevel),
                result.IsAmbiguous);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException)
        {
            // Recognition must never make a discovered media item unusable.
            // SourceTitle and Location remain sufficient for raw playback.
            return null;
        }
    }

    private static CatalogRecognitionMediaKind MapMediaKind(
        Eizo.Metadata.Recognition.MediaKind kind) =>
        kind switch
        {
            Eizo.Metadata.Recognition.MediaKind.SeriesEpisode =>
                CatalogRecognitionMediaKind.SeriesEpisode,
            Eizo.Metadata.Recognition.MediaKind.Movie =>
                CatalogRecognitionMediaKind.Movie,
            Eizo.Metadata.Recognition.MediaKind.Special =>
                CatalogRecognitionMediaKind.Special,
            _ =>
                CatalogRecognitionMediaKind.Unknown
        };

    private static CatalogRecognitionSpecialKind MapSpecialKind(
        Eizo.Metadata.Recognition.SpecialKind kind) =>
        kind switch
        {
            Eizo.Metadata.Recognition.SpecialKind.Ova =>
                CatalogRecognitionSpecialKind.Ova,
            Eizo.Metadata.Recognition.SpecialKind.Oad =>
                CatalogRecognitionSpecialKind.Oad,
            Eizo.Metadata.Recognition.SpecialKind.Ona =>
                CatalogRecognitionSpecialKind.Ona,
            Eizo.Metadata.Recognition.SpecialKind.Special =>
                CatalogRecognitionSpecialKind.Special,
            Eizo.Metadata.Recognition.SpecialKind.NcOp =>
                CatalogRecognitionSpecialKind.NcOp,
            Eizo.Metadata.Recognition.SpecialKind.NcEd =>
                CatalogRecognitionSpecialKind.NcEd,
            _ =>
                CatalogRecognitionSpecialKind.None
        };

    private static CatalogRecognitionEpisodePart MapEpisodePart(
        Eizo.Metadata.Recognition.EpisodePart part) =>
        part switch
        {
            Eizo.Metadata.Recognition.EpisodePart.First =>
                CatalogRecognitionEpisodePart.First,
            Eizo.Metadata.Recognition.EpisodePart.Second =>
                CatalogRecognitionEpisodePart.Second,
            _ =>
                CatalogRecognitionEpisodePart.None
        };

    private static CatalogRecognitionConfidenceLevel MapConfidenceLevel(
        RecognitionConfidenceLevel level) =>
        level switch
        {
            RecognitionConfidenceLevel.Low =>
                CatalogRecognitionConfidenceLevel.Low,
            RecognitionConfidenceLevel.Medium =>
                CatalogRecognitionConfidenceLevel.Medium,
            RecognitionConfidenceLevel.High =>
                CatalogRecognitionConfidenceLevel.High,
            _ =>
                CatalogRecognitionConfidenceLevel.None
        };

    private static IEnumerable<string> EnumerateVideoFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current).ToArray();
            }
            catch (IOException)
            {
                files = [];
            }
            catch (UnauthorizedAccessException)
            {
                files = [];
            }

            foreach (var file in files)
            {
                if (IsSupportedVideoPath(file))
                    yield return file;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current).ToArray();
            }
            catch (IOException)
            {
                directories = [];
            }
            catch (UnauthorizedAccessException)
            {
                directories = [];
            }

            foreach (var directory in directories)
            {
                try
                {
                    if ((File.GetAttributes(directory) &
                         FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                pending.Push(directory);
            }
        }
    }

    private static bool IsAvailable(CatalogMediaItemModel item) =>
        item.Location switch
        {
            { Kind: MediaLocationKind.LocalFile } location =>
                File.Exists(location.Locator),
            { Kind: MediaLocationKind.RemoteUri } =>
                true,
            null =>
                false,
            _ =>
                false
        };

    private bool PruneMissingLocalFilesCore()
    {
        return _items.RemoveAll(item =>
            item.Location is
            {
                Kind: MediaLocationKind.LocalFile
            } location &&
            !File.Exists(location.Locator)) > 0;
    }

    private static List<CatalogMediaItemModel> LoadCore()
    {
        try
        {
            if (!File.Exists(StorePath))
                return [];

            var document =
                JsonSerializer.Deserialize<MediaCatalogStoreDocument>(
                    File.ReadAllText(StorePath),
                    SerializerOptions);

            return document is not null &&
                   document.SchemaVersion is 1 or 2
                ? document.Items ?? []
                : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void SaveCore(IReadOnlyList<CatalogMediaItemModel> items)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var temporaryPath =
                $"{StorePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

            var document = new MediaCatalogStoreDocument(
                SchemaVersion: 2,
                Items: items.ToList());

            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(document, SerializerOptions));

            File.Move(temporaryPath, StorePath, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record MediaCatalogStoreDocument(
        int SchemaVersion,
        List<CatalogMediaItemModel> Items);
}
