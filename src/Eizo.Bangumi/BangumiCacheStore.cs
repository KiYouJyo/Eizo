using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Eizo.Bangumi;

internal sealed record BangumiCachedPayload(
    DateTimeOffset FetchedAtUtc,
    string Payload);

internal sealed class BangumiCacheStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = false };

    private readonly string _cacheRoot;

    public BangumiCacheStore(string? cacheRoot = null)
    {
        _cacheRoot = cacheRoot ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Eizo",
            "Bangumi",
            "Cache");
    }

    public async Task<BangumiCachedPayload?> ReadAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(
                path,
                cancellationToken);
            return JsonSerializer.Deserialize<BangumiCachedPayload>(
                json,
                SerializerOptions);
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

    public async Task WriteAsync(
        string key,
        BangumiCachedPayload payload,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_cacheRoot);

        var path = ResolvePath(key);
        var temporaryPath = path + ".tmp";
        var json = JsonSerializer.Serialize(
            payload,
            SerializerOptions);

        await File.WriteAllTextAsync(
            temporaryPath,
            json,
            Encoding.UTF8,
            cancellationToken);
        File.Move(
            temporaryPath,
            path,
            overwrite: true);
    }

    private string ResolvePath(string key)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(key));
        var fileName =
            Convert.ToHexString(bytes).ToLowerInvariant() + ".json";
        return Path.Combine(_cacheRoot, fileName);
    }
}
