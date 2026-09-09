using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Eizo.Models;

public enum MediaSourceKind
{
    Local = 0,
    WebDav = 1
}

public sealed record MediaSourceDefinition(
    string Id,
    MediaSourceKind Kind,
    string DisplayName,
    string? RootLocation = null,
    string? UserName = null,
    string? CredentialKey = null,
    string? AccessToken = null,
    bool Enabled = true,
    DateTimeOffset? LastScanUtc = null)
{
    public const string OpenedLocalFilesSourceId = "local-opened-files";

    public bool IsBuiltIn =>
        string.Equals(
            Id,
            OpenedLocalFilesSourceId,
            StringComparison.Ordinal);
}

public sealed class MediaSourceStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = true };

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "sources.json");

    private readonly object _sync = new();
    private readonly List<MediaSourceDefinition> _sources;

    private MediaSourceStore()
    {
        _sources = LoadCore();

        if (_sources.All(source =>
                !string.Equals(
                    source.Id,
                    MediaSourceDefinition.OpenedLocalFilesSourceId,
                    StringComparison.Ordinal)))
        {
            _sources.Insert(
                0,
                new MediaSourceDefinition(
                    MediaSourceDefinition.OpenedLocalFilesSourceId,
                    MediaSourceKind.Local,
                    "Local media"));
            SaveCore(_sources);
        }
    }

    public static MediaSourceStore Default { get; } = new();

    public event EventHandler? Changed;

    public IReadOnlyList<MediaSourceDefinition> Snapshot()
    {
        lock (_sync)
            return _sources.ToArray();
    }

    public MediaSourceDefinition? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        lock (_sync)
            return _sources.FirstOrDefault(source =>
                string.Equals(source.Id, id, StringComparison.Ordinal));
    }

    public MediaSourceDefinition AddLocalFolder(
        string folderPath,
        string? displayName = null,
        string? accessToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        var fullPath = Path.GetFullPath(folderPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException(fullPath);

        var id = BuildLocalFolderId(fullPath);
        var name = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(fullPath)
            : displayName.Trim();

        if (string.IsNullOrWhiteSpace(name))
            name = fullPath;

        MediaSourceDefinition result;

        lock (_sync)
        {
            var index = _sources.FindIndex(source =>
                string.Equals(source.Id, id, StringComparison.Ordinal));

            result = new MediaSourceDefinition(
                id,
                MediaSourceKind.Local,
                name,
                fullPath,
                UserName: index >= 0
                    ? _sources[index].UserName
                    : null,
                CredentialKey: index >= 0
                    ? _sources[index].CredentialKey
                    : null,
                AccessToken: !string.IsNullOrWhiteSpace(accessToken)
                    ? accessToken
                    : index >= 0
                        ? _sources[index].AccessToken
                        : null,
                Enabled: true,
                LastScanUtc: index >= 0
                    ? _sources[index].LastScanUtc
                    : null);

            if (index >= 0)
                _sources[index] = result;
            else
                _sources.Add(result);

            SaveCore(_sources);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public void MarkScanned(string id, DateTimeOffset timestamp)
    {
        lock (_sync)
        {
            var index = _sources.FindIndex(source =>
                string.Equals(source.Id, id, StringComparison.Ordinal));

            if (index < 0)
                return;

            _sources[index] = _sources[index] with
            {
                LastScanUtc = timestamp
            };

            SaveCore(_sources);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(string id)
    {
        if (string.Equals(
                id,
                MediaSourceDefinition.OpenedLocalFilesSourceId,
                StringComparison.Ordinal))
        {
            return false;
        }

        bool removed;
        lock (_sync)
        {
            removed = _sources.RemoveAll(source =>
                string.Equals(source.Id, id, StringComparison.Ordinal)) > 0;

            if (removed)
                SaveCore(_sources);
        }

        if (removed)
            Changed?.Invoke(this, EventArgs.Empty);

        return removed;
    }

    public string ResolveLocalSourceId(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);

        lock (_sync)
        {
            var matchingSource = _sources
                .Where(source =>
                    source.Kind == MediaSourceKind.Local &&
                    !string.IsNullOrWhiteSpace(source.RootLocation) &&
                    IsPathWithinRoot(fullPath, source.RootLocation!))
                .OrderByDescending(source => source.RootLocation!.Length)
                .FirstOrDefault();

            return matchingSource?.Id ??
                   MediaSourceDefinition.OpenedLocalFilesSourceId;
        }
    }

    private static bool IsPathWithinRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var normalizedPath = Path.GetFullPath(path);

        return normalizedPath.StartsWith(
            normalizedRoot,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildLocalFolderId(string fullPath)
    {
        var normalized = fullPath.ToUpperInvariant();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "local-folder-" +
               Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();
    }

    private static List<MediaSourceDefinition> LoadCore()
    {
        try
        {
            if (!File.Exists(StorePath))
                return [];

            var json = File.ReadAllText(StorePath);
            var document =
                JsonSerializer.Deserialize<MediaSourceStoreDocument>(
                    json,
                    SerializerOptions);

            if (document is { SchemaVersion: 1 })
                return document.Sources ?? [];

            return [];
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

    private static void SaveCore(IReadOnlyList<MediaSourceDefinition> sources)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var temporaryPath =
                $"{StorePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

            var document = new MediaSourceStoreDocument(
                SchemaVersion: 1,
                Sources: sources.ToList());

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

    private sealed record MediaSourceStoreDocument(
        int SchemaVersion,
        List<MediaSourceDefinition> Sources);
}
