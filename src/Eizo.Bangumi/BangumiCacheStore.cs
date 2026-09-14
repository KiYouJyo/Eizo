using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Eizo.Cache;

namespace Eizo.Bangumi;

internal sealed record BangumiCachedPayload(
    DateTimeOffset FetchedAtUtc,
    string Payload);

internal sealed class BangumiCacheStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = false };

    private readonly DiskCacheStore _cache;
    private readonly string? _legacyCacheRoot;

    public BangumiCacheStore(string? cacheRoot = null)
    {
        _cache = new DiskCacheStore(cacheRoot);

        if (cacheRoot is null)
        {
            _legacyCacheRoot = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Eizo",
                "Bangumi",
                "Cache");
        }
    }

    public async Task<BangumiCachedPayload?> ReadAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(key);
        var json = await _cache.ReadTextAsync(
            CacheCategory.Metadata,
            cacheKey,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                return JsonSerializer.Deserialize<BangumiCachedPayload>(
                    json,
                    SerializerOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return await TryMigrateLegacyAsync(
            key,
            cacheKey,
            cancellationToken);
    }

    public async Task WriteAsync(
        string key,
        BangumiCachedPayload payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(
            payload,
            SerializerOptions);

        await _cache.WriteTextAsync(
            CacheCategory.Metadata,
            BuildCacheKey(key),
            json,
            new CacheWriteOptions(
                BuildDisplayName(key),
                "Bangumi",
                ".json"),
            cancellationToken);
    }

    private async Task<BangumiCachedPayload?> TryMigrateLegacyAsync(
        string key,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_legacyCacheRoot))
            return null;

        var path = ResolveLegacyPath(key);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(
                path,
                cancellationToken);
            var payload =
                JsonSerializer.Deserialize<BangumiCachedPayload>(
                    json,
                    SerializerOptions);

            if (payload is null)
                return null;

            await _cache.WriteTextAsync(
                CacheCategory.Metadata,
                cacheKey,
                json,
                new CacheWriteOptions(
                    BuildDisplayName(key),
                    "Bangumi",
                    ".json"),
                cancellationToken);

            try
            {
                File.Delete(path);
            }
            catch
            {
            }

            return payload;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private string ResolveLegacyPath(string key)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(key));
        var fileName =
            Convert.ToHexString(bytes)
                .ToLowerInvariant() +
            ".json";
        return Path.Combine(
            _legacyCacheRoot!,
            fileName);
    }

    private static string BuildCacheKey(
        string key) =>
        "bangumi:" + key;

    private static string BuildDisplayName(
        string key)
    {
        var normalized = key.Replace(
            ':',
            ' ');
        if (normalized.Length > 72)
            normalized = normalized[..72] + "…";

        return "Bangumi API · " + normalized;
    }
}
