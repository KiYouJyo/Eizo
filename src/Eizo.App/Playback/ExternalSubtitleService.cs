using System.Security.Cryptography;
using System.Text;
using Eizo.Models;
using Eizo.Playback;

namespace Eizo.PlaybackSupport;

internal sealed record ExternalSubtitleCandidate(
    Uri Uri,
    string DisplayName,
    string? Language,
    string Extension);

internal static class ExternalSubtitleService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".srt",
            ".vtt",
            ".ass",
            ".ssa",
        };

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "SubtitleCache");

    internal static async Task<IReadOnlyList<ExternalSubtitleCandidate>> DiscoverAsync(
        PlaybackSource source,
        CatalogMediaItemModel? catalogItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Uri.IsFile)
            return DiscoverLocal(source.Uri.LocalPath);

        if (catalogItem?.Location is not
            {
                Kind: MediaLocationKind.RemoteUri
            } location)
        {
            return [];
        }

        var definition = MediaSourceStore.Default.Find(location.SourceId);
        if (definition is not
            {
                Kind: MediaSourceKind.WebDav,
                RootLocation.Length: > 0
            } webDavSource ||
            !Uri.TryCreate(
                webDavSource.RootLocation,
                UriKind.Absolute,
                out var rootUri) ||
            !Uri.TryCreate(
                location.Locator,
                UriKind.Absolute,
                out var mediaUri) ||
            !MediaSourceProviderRegistry.TryGet(
                MediaSourceKind.WebDav,
                out var provider) ||
            provider is not WebDavMediaSourceProvider webDavProvider)
        {
            return [];
        }

        var mediaName = Path.GetFileName(
            Uri.UnescapeDataString(mediaUri.AbsolutePath));
        var relative = rootUri.MakeRelativeUri(mediaUri)
            .ToString()
            .Split('?', '#')[0];
        relative = Uri.UnescapeDataString(relative);

        var separator = relative.LastIndexOf('/');
        var parent = separator >= 0
            ? relative[..(separator + 1)]
            : string.Empty;

        var discovered = new List<ExternalSubtitleCandidate>();

        try
        {
            await foreach (var entry in webDavProvider.ListAsync(
                               webDavSource,
                               parent,
                               cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsDirectory ||
                    !IsMatchingSubtitle(
                        entry.Name,
                        mediaName) ||
                    string.IsNullOrWhiteSpace(entry.Locator) ||
                    !Uri.TryCreate(
                        entry.Locator,
                        UriKind.Absolute,
                        out var subtitleUri))
                {
                    continue;
                }

                try
                {
                    var cachedUri = await MaterializeRemoteAsync(
                        webDavProvider,
                        webDavSource,
                        subtitleUri,
                        cancellationToken);

                    if (cachedUri is null)
                        continue;

                    discovered.Add(CreateCandidate(
                        cachedUri,
                        entry.Name));
                }
                catch (Exception exception)
                    when (exception is
                        IOException or
                        UnauthorizedAccessException or
                        HttpRequestException or
                        MediaSourceException)
                {
                    // A missing optional subtitle must never block media playback.
                }
            }
        }
        catch (Exception exception)
            when (exception is
                IOException or
                UnauthorizedAccessException or
                HttpRequestException or
                MediaSourceException)
        {
            return [];
        }

        return OrderCandidates(discovered, mediaName);
    }

    internal static async Task<SubtitleDocument> LoadDocumentAsync(
        ExternalSubtitleCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!candidate.Uri.IsFile ||
            !File.Exists(candidate.Uri.LocalPath))
        {
            return new SubtitleDocument([]);
        }

        try
        {
            await using var stream = File.OpenRead(candidate.Uri.LocalPath);
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);

            var text = await reader.ReadToEndAsync(cancellationToken);
            return SubtitleTextParser.Parse(
                candidate.Extension,
                text);
        }
        catch (Exception exception)
            when (exception is
                IOException or
                UnauthorizedAccessException or
                DecoderFallbackException)
        {
            return new SubtitleDocument([]);
        }
    }

    private static IReadOnlyList<ExternalSubtitleCandidate> DiscoverLocal(
        string mediaPath)
    {
        var directory = Path.GetDirectoryName(mediaPath);
        var mediaName = Path.GetFileName(mediaPath);

        if (string.IsNullOrWhiteSpace(directory) ||
            !Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            var candidates = Directory
                .EnumerateFiles(
                    directory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(path => IsMatchingSubtitle(
                    Path.GetFileName(path),
                    mediaName))
                .Select(path => CreateCandidate(
                    new Uri(Path.GetFullPath(path)),
                    Path.GetFileName(path)))
                .ToArray();

            return OrderCandidates(candidates, mediaName);
        }
        catch (Exception exception)
            when (exception is
                IOException or
                UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static ExternalSubtitleCandidate CreateCandidate(
        Uri uri,
        string fileName)
    {
        var extension = Path.GetExtension(fileName)
            .ToLowerInvariant();

        return new ExternalSubtitleCandidate(
            uri,
            Path.GetFileNameWithoutExtension(fileName),
            InferLanguage(fileName),
            extension);
    }

    private static IReadOnlyList<ExternalSubtitleCandidate> OrderCandidates(
        IEnumerable<ExternalSubtitleCandidate> candidates,
        string mediaName)
    {
        var mediaStem = Path.GetFileNameWithoutExtension(mediaName);

        return candidates
            .DistinctBy(static candidate => candidate.Uri.AbsoluteUri)
            .OrderByDescending(candidate =>
                string.Equals(
                    candidate.DisplayName,
                    mediaStem,
                    StringComparison.OrdinalIgnoreCase))
            .ThenBy(static candidate => LanguagePriority(candidate.Language))
            .ThenBy(
                static candidate => candidate.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static bool IsMatchingSubtitle(
        string subtitleName,
        string mediaName)
    {
        var extension = Path.GetExtension(subtitleName);
        if (!SupportedExtensions.Contains(extension))
            return false;

        var subtitleStem = Path.GetFileNameWithoutExtension(subtitleName);
        var mediaStem = Path.GetFileNameWithoutExtension(mediaName);

        if (string.IsNullOrWhiteSpace(subtitleStem) ||
            string.IsNullOrWhiteSpace(mediaStem))
        {
            return false;
        }

        if (string.Equals(
                subtitleStem,
                mediaStem,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var separator in new[] { ".", " ", "_", "-" })
        {
            if (subtitleStem.StartsWith(
                    mediaStem + separator,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<Uri?> MaterializeRemoteAsync(
        WebDavMediaSourceProvider provider,
        MediaSourceDefinition source,
        Uri subtitleUri,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(
            Uri.UnescapeDataString(subtitleUri.AbsolutePath));

        if (!SupportedExtensions.Contains(extension))
            return null;

        Directory.CreateDirectory(CacheDirectory);

        var digest = SHA256.HashData(
            Encoding.UTF8.GetBytes(subtitleUri.AbsoluteUri));
        var fileName =
            Convert.ToHexString(digest)
                .ToLowerInvariant() +
            extension.ToLowerInvariant();
        var path = Path.Combine(
            CacheDirectory,
            fileName);

        if (!File.Exists(path))
        {
            var bytes = await provider.DownloadFileAsync(
                source,
                subtitleUri,
                cancellationToken);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                await File.WriteAllBytesAsync(
                    temporary,
                    bytes,
                    cancellationToken);
                File.Move(
                    temporary,
                    path,
                    overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch
                {
                }
            }
        }

        return new Uri(Path.GetFullPath(path));
    }

    private static string? InferLanguage(string fileName)
    {
        var value = (
            "." +
            Path.GetFileNameWithoutExtension(fileName)
                .ToLowerInvariant()
                .Replace('_', '.')
                .Replace('-', '.') +
            ".");

        if (ContainsAny(
                value,
                ".zh.cn.",
                ".zh.hans.",
                ".chs.",
                ".sc.",
                ".简体."))
        {
            return "zh-CN";
        }

        if (ContainsAny(
                value,
                ".zh.tw.",
                ".zh.hant.",
                ".cht.",
                ".tc.",
                ".繁体.",
                ".繁體."))
        {
            return "zh-TW";
        }

        if (ContainsAny(
                value,
                ".ja.",
                ".jp.",
                ".jpn.",
                ".日本語."))
        {
            return "ja";
        }

        if (ContainsAny(
                value,
                ".en.",
                ".eng.",
                ".english."))
        {
            return "en";
        }

        if (value.Contains(".zh.", StringComparison.Ordinal))
            return "zh";

        return null;
    }

    private static bool ContainsAny(
        string value,
        params string[] tokens) =>
        tokens.Any(token =>
            value.Contains(
                token,
                StringComparison.Ordinal));

    private static int LanguagePriority(string? language) =>
        language switch
        {
            "zh-CN" => 0,
            "zh-TW" => 1,
            "zh" => 2,
            "ja" => 3,
            "en" => 4,
            _ => 10,
        };
}
