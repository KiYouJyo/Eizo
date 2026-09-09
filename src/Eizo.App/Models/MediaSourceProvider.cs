namespace Eizo.Models;

public sealed record MediaSourceConnectionResult(
    bool IsAvailable,
    string? ErrorCode = null,
    string? Detail = null);

public sealed record MediaSourceEntry(
    string Name,
    string RelativePath,
    bool IsDirectory,
    long? SizeBytes = null,
    DateTimeOffset? ModifiedUtc = null,
    string? Locator = null,
    string? ETag = null);

public sealed class MediaSourceException(
    string errorCode,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}

public interface IMediaSourceProvider
{
    MediaSourceKind Kind { get; }

    ValueTask<MediaSourceConnectionResult> TestConnectionAsync(
        MediaSourceDefinition source,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<MediaSourceEntry> ListAsync(
        MediaSourceDefinition source,
        string relativePath = "",
        CancellationToken cancellationToken = default);
}

public sealed class LocalMediaSourceProvider : IMediaSourceProvider
{
    public MediaSourceKind Kind => MediaSourceKind.Local;

    public ValueTask<MediaSourceConnectionResult> TestConnectionAsync(
        MediaSourceDefinition source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        if (source.Kind != MediaSourceKind.Local)
        {
            return ValueTask.FromResult(
                new MediaSourceConnectionResult(
                    false,
                    "SourceKindMismatch"));
        }

        if (source.IsBuiltIn)
        {
            return ValueTask.FromResult(
                new MediaSourceConnectionResult(true));
        }

        if (string.IsNullOrWhiteSpace(source.RootLocation))
        {
            return ValueTask.FromResult(
                new MediaSourceConnectionResult(
                    false,
                    "SourceRootMissing"));
        }

        return ValueTask.FromResult(
            Directory.Exists(source.RootLocation)
                ? new MediaSourceConnectionResult(true)
                : new MediaSourceConnectionResult(
                    false,
                    "SourceUnavailable"));
    }

    public async IAsyncEnumerable<MediaSourceEntry> ListAsync(
        MediaSourceDefinition source,
        string relativePath = "",
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Kind != MediaSourceKind.Local ||
            string.IsNullOrWhiteSpace(source.RootLocation))
        {
            yield break;
        }

        var root = Path.GetFullPath(source.RootLocation);
        var current = string.IsNullOrWhiteSpace(relativePath)
            ? root
            : Path.GetFullPath(
                Path.Combine(root, relativePath));

        if (!IsWithinRoot(current, root) ||
            !Directory.Exists(current))
        {
            yield break;
        }

        string[] directories;
        string[] files;

        try
        {
            directories = Directory.GetDirectories(current);
            files = Directory.GetFiles(current);
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new DirectoryInfo(directory);
            yield return new MediaSourceEntry(
                info.Name,
                Path.GetRelativePath(root, directory),
                IsDirectory: true,
                ModifiedUtc: info.LastWriteTimeUtc);

            await Task.Yield();
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new FileInfo(file);
            yield return new MediaSourceEntry(
                info.Name,
                Path.GetRelativePath(root, file),
                IsDirectory: false,
                SizeBytes: info.Exists ? info.Length : null,
                ModifiedUtc: info.Exists
                    ? info.LastWriteTimeUtc
                    : null);

            await Task.Yield();
        }
    }

    private static bool IsWithinRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var normalizedPath = Path.GetFullPath(path);

        return string.Equals(
                   normalizedPath.TrimEnd(
                       Path.DirectorySeparatorChar,
                       Path.AltDirectorySeparatorChar),
                   root.TrimEnd(
                       Path.DirectorySeparatorChar,
                       Path.AltDirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(
                   normalizedRoot,
                   StringComparison.OrdinalIgnoreCase);
    }
}

public static class MediaSourceProviderRegistry
{
    private static readonly IReadOnlyDictionary<MediaSourceKind, IMediaSourceProvider>
        Providers =
            new Dictionary<MediaSourceKind, IMediaSourceProvider>
            {
                [MediaSourceKind.Local] =
                    new LocalMediaSourceProvider(),
                [MediaSourceKind.WebDav] =
                    new WebDavMediaSourceProvider(
                        MediaCredentialStore.Default)
            };

    public static bool TryGet(
        MediaSourceKind kind,
        out IMediaSourceProvider provider) =>
        Providers.TryGetValue(kind, out provider!);
}
