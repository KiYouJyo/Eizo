namespace Eizo.Models;

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
