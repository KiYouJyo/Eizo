using System.Text.Json;
using Eizo.MetadataIntegration;

namespace Eizo.Models;

public sealed record MediaIdentityBindingRecord(
    string EizoMediaId,
    Dictionary<string, string> ExternalIds,
    string? PrimaryProvider,
    HashSet<string> ManualProviders,
    DateTimeOffset UpdatedAtUtc)
{
    public bool IsManual => ManualProviders.Count > 0;
}

public sealed class MediaIdentityBindingStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = true };

    private static readonly string DefaultStorePath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "media-identity-bindings.json");

    private readonly object _sync = new();
    private readonly string _path;
    private readonly Dictionary<string, MediaIdentityBindingRecord>
        _bindings;

    public MediaIdentityBindingStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? DefaultStorePath
            : Path.GetFullPath(path);
        _bindings = Load(_path);
    }

    public static MediaIdentityBindingStore Default { get; } = new();

    public MediaIdentityBindingHint? GetHint(string? eizoMediaId)
    {
        if (string.IsNullOrWhiteSpace(eizoMediaId))
            return null;

        lock (_sync)
        {
            if (!_bindings.TryGetValue(
                    eizoMediaId,
                    out var record))
            {
                return null;
            }

            return new MediaIdentityBindingHint(
                record.EizoMediaId,
                record.PrimaryProvider,
                new Dictionary<string, string>(
                    record.ExternalIds,
                    StringComparer.OrdinalIgnoreCase),
                record.IsManual);
        }
    }

    public MediaIdentityBindingHint? GetTmdbHint(
        string? eizoMediaId)
    {
        if (string.IsNullOrWhiteSpace(eizoMediaId))
            return null;

        lock (_sync)
        {
            if (!_bindings.TryGetValue(
                    eizoMediaId,
                    out var record) ||
                !record.ExternalIds.TryGetValue(
                    "tmdb",
                    out var tmdbId) ||
                string.IsNullOrWhiteSpace(tmdbId))
            {
                return null;
            }

            return new MediaIdentityBindingHint(
                record.EizoMediaId,
                "tmdb",
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["tmdb"] = tmdbId.Trim(),
                },
                record.ManualProviders.Contains("tmdb"));
        }
    }

    public IReadOnlyList<MediaIdentityBindingRecord> Snapshot()
    {
        lock (_sync)
        {
            return _bindings.Values
                .OrderBy(static item => item.EizoMediaId)
                .Select(Clone)
                .ToArray();
        }
    }

    public void UpsertAutomatic(
        string? eizoMediaId,
        MediaMetadataSnapshot metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (string.IsNullOrWhiteSpace(eizoMediaId) ||
            !metadata.IsResolved)
        {
            return;
        }

        lock (_sync)
        {
            _bindings.TryGetValue(
                eizoMediaId,
                out var existing);

            var ids = existing is null
                ? new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(
                    existing.ExternalIds,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var pair in metadata.ExternalIds)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) ||
                    string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                if (existing?.ManualProviders.Contains(
                        pair.Key) == true)
                {
                    continue;
                }

                ids[pair.Key.Trim().ToLowerInvariant()] =
                    pair.Value.Trim();
            }

            if (!string.IsNullOrWhiteSpace(metadata.Provider) &&
                !string.IsNullOrWhiteSpace(
                    metadata.ProviderSubjectId) &&
                existing?.ManualProviders.Contains(
                    metadata.Provider) != true)
            {
                ids[metadata.Provider.Trim().ToLowerInvariant()] =
                    metadata.ProviderSubjectId.Trim();
            }

            var manualProviders =
                existing?.ManualProviders is null
                    ? new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(
                        existing.ManualProviders,
                        StringComparer.OrdinalIgnoreCase);

            var primary = existing?.PrimaryProvider;
            if (string.Equals(
                    metadata.Provider,
                    "tmdb",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Bangumi no longer participates in local-library identity.
                ids.Remove("bangumi");
                manualProviders.Remove("bangumi");
                primary = "tmdb";
            }
            else if (string.IsNullOrWhiteSpace(primary) &&
                     !string.IsNullOrWhiteSpace(metadata.Provider))
            {
                primary = metadata.Provider.Trim().ToLowerInvariant();
            }

            _bindings[eizoMediaId] =
                new MediaIdentityBindingRecord(
                    eizoMediaId,
                    ids,
                    primary,
                    manualProviders,
                    DateTimeOffset.UtcNow);
            SaveCore();
        }
    }

    public void SetManual(
        string eizoMediaId,
        string provider,
        string providerSubjectId,
        bool makePrimary = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eizoMediaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSubjectId);

        var normalizedProvider =
            provider.Trim().ToLowerInvariant();

        lock (_sync)
        {
            _bindings.TryGetValue(
                eizoMediaId,
                out var existing);

            var ids = existing is null
                ? new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(
                    existing.ExternalIds,
                    StringComparer.OrdinalIgnoreCase);
            ids[normalizedProvider] =
                providerSubjectId.Trim();

            var manualProviders =
                existing?.ManualProviders is null
                    ? new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(
                        existing.ManualProviders,
                        StringComparer.OrdinalIgnoreCase);
            manualProviders.Add(normalizedProvider);

            _bindings[eizoMediaId] =
                new MediaIdentityBindingRecord(
                    eizoMediaId,
                    ids,
                    makePrimary
                        ? normalizedProvider
                        : existing?.PrimaryProvider,
                    manualProviders,
                    DateTimeOffset.UtcNow);
            SaveCore();
        }
    }

    public void ClearManual(
        string eizoMediaId,
        string provider,
        bool removeExternalId = false)
    {
        if (string.IsNullOrWhiteSpace(eizoMediaId) ||
            string.IsNullOrWhiteSpace(provider))
        {
            return;
        }

        var normalizedProvider =
            provider.Trim().ToLowerInvariant();

        lock (_sync)
        {
            if (!_bindings.TryGetValue(
                    eizoMediaId,
                    out var existing))
            {
                return;
            }

            var ids = new Dictionary<string, string>(
                existing.ExternalIds,
                StringComparer.OrdinalIgnoreCase);
            if (removeExternalId)
                ids.Remove(normalizedProvider);

            var manualProviders = new HashSet<string>(
                existing.ManualProviders,
                StringComparer.OrdinalIgnoreCase);
            manualProviders.Remove(normalizedProvider);

            var primary = existing.PrimaryProvider;
            if (string.Equals(
                    primary,
                    normalizedProvider,
                    StringComparison.OrdinalIgnoreCase) &&
                removeExternalId)
            {
                primary = ids.Keys.FirstOrDefault();
            }

            if (ids.Count == 0 &&
                manualProviders.Count == 0)
            {
                _bindings.Remove(eizoMediaId);
            }
            else
            {
                _bindings[eizoMediaId] =
                    existing with
                    {
                        ExternalIds = ids,
                        PrimaryProvider = primary,
                        ManualProviders = manualProviders,
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                    };
            }

            SaveCore();
        }
    }

    private void SaveCore()
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(_path)!);
            var temporary =
                $"{_path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            var document = new BindingDocument(
                1,
                _bindings.Values
                    .OrderBy(static item => item.EizoMediaId)
                    .ToList());
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    document,
                    SerializerOptions));
            File.Move(
                temporary,
                _path,
                overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static Dictionary<string, MediaIdentityBindingRecord>
        Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, MediaIdentityBindingRecord>(
                    StringComparer.OrdinalIgnoreCase);
            }

            var document =
                JsonSerializer.Deserialize<BindingDocument>(
                    File.ReadAllText(path),
                    SerializerOptions);
            if (document is not { SchemaVersion: 1 })
            {
                return new Dictionary<string, MediaIdentityBindingRecord>(
                    StringComparer.OrdinalIgnoreCase);
            }

            return (document.Bindings ?? [])
                .Where(static item =>
                    !string.IsNullOrWhiteSpace(item.EizoMediaId))
                .ToDictionary(
                    static item => item.EizoMediaId,
                    Clone,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return new Dictionary<string, MediaIdentityBindingRecord>(
                StringComparer.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException)
        {
            return new Dictionary<string, MediaIdentityBindingRecord>(
                StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, MediaIdentityBindingRecord>(
                StringComparer.OrdinalIgnoreCase);
        }
    }

    private static MediaIdentityBindingRecord Clone(
        MediaIdentityBindingRecord item) =>
        item with
        {
            ExternalIds = new Dictionary<string, string>(
                item.ExternalIds,
                StringComparer.OrdinalIgnoreCase),
            ManualProviders = new HashSet<string>(
                item.ManualProviders,
                StringComparer.OrdinalIgnoreCase),
        };

    private sealed record BindingDocument(
        int SchemaVersion,
        List<MediaIdentityBindingRecord> Bindings);
}
