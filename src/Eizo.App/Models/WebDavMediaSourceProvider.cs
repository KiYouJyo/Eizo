using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace Eizo.Models;

public sealed record WebDavMediaProbeResult(
    bool IsAvailable,
    bool SupportsRanges,
    long? ContentLength = null,
    string? ErrorCode = null,
    string? Detail = null);

public sealed class WebDavMediaSourceProvider(
    MediaCredentialStore credentialStore)
    : IMediaSourceProvider
{
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");
    private static readonly XNamespace Dav = "DAV:";

    private readonly MediaCredentialStore _credentialStore =
        credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));

    public MediaSourceKind Kind => MediaSourceKind.WebDav;

    public async ValueTask<MediaSourceConnectionResult> TestConnectionAsync(
        MediaSourceDefinition source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateSource(source);
            using var client = CreateClient(source);
            using var request = CreatePropFindRequest(
                new Uri(source.RootLocation!, UriKind.Absolute),
                depth: 0);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.MultiStatus ||
                response.IsSuccessStatusCode)
            {
                return new MediaSourceConnectionResult(true);
            }

            return new MediaSourceConnectionResult(
                false,
                MapStatusCode(response.StatusCode),
                $"{(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new MediaSourceConnectionResult(
                false,
                "Timeout");
        }
        catch (HttpRequestException exception)
        {
            return new MediaSourceConnectionResult(
                false,
                "NetworkError",
                exception.Message);
        }
        catch (MediaSourceException exception)
        {
            return new MediaSourceConnectionResult(
                false,
                exception.ErrorCode,
                exception.Message);
        }
    }

    public async IAsyncEnumerable<MediaSourceEntry> ListAsync(
        MediaSourceDefinition source,
        string relativePath = "",
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ValidateSource(source);

        var rootUri = new Uri(source.RootLocation!, UriKind.Absolute);
        var requestUri = BuildRequestUri(rootUri, relativePath);

        using var client = CreateClient(source);
        using var request = CreatePropFindRequest(requestUri, depth: 1);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode != HttpStatusCode.MultiStatus &&
            !response.IsSuccessStatusCode)
        {
            throw new MediaSourceException(
                MapStatusCode(response.StatusCode),
                $"WebDAV PROPFIND failed with {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        XDocument document;

        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (Exception exception) when (
            exception is System.Xml.XmlException or InvalidOperationException)
        {
            throw new MediaSourceException(
                "InvalidWebDavResponse",
                "The WebDAV server returned an invalid multistatus document.",
                exception);
        }

        foreach (var responseElement in document.Descendants(Dav + "response"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var href = responseElement.Element(Dav + "href")?.Value;
            if (string.IsNullOrWhiteSpace(href))
                continue;

            var itemUri = ResolveHref(requestUri, href);
            if (itemUri is null ||
                !IsUriWithinRoot(rootUri, itemUri) ||
                SameResource(itemUri, requestUri))
            {
                continue;
            }

            var prop = SelectSuccessfulPropertySet(responseElement);
            if (prop is null)
                continue;

            var isDirectory =
                prop.Element(Dav + "resourcetype")?
                    .Element(Dav + "collection") is not null;

            var relative = GetRelativePath(rootUri, itemUri);
            if (string.IsNullOrWhiteSpace(relative))
                continue;

            if (isDirectory && !relative.EndsWith("/", StringComparison.Ordinal))
                relative += "/";

            var trimmed = relative.TrimEnd('/');
            var separator = trimmed.LastIndexOf('/');
            var name = separator >= 0
                ? trimmed[(separator + 1)..]
                : trimmed;

            long? size = null;
            if (long.TryParse(
                    prop.Element(Dav + "getcontentlength")?.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsedSize))
            {
                size = parsedSize;
            }

            DateTimeOffset? modified = null;
            if (DateTimeOffset.TryParse(
                    prop.Element(Dav + "getlastmodified")?.Value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsedModified))
            {
                modified = parsedModified;
            }

            yield return new MediaSourceEntry(
                Uri.UnescapeDataString(name),
                relative,
                isDirectory,
                size,
                modified,
                itemUri.AbsoluteUri,
                prop.Element(Dav + "getetag")?.Value);

            await Task.Yield();
        }
    }

    public async Task<WebDavMediaProbeResult> ProbeMediaAsync(
        MediaSourceDefinition source,
        Uri mediaUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateSource(source);

            var rootUri = new Uri(source.RootLocation!, UriKind.Absolute);
            if (!mediaUri.IsAbsoluteUri ||
                !IsUriWithinRoot(rootUri, mediaUri))
            {
                return new WebDavMediaProbeResult(
                    false,
                    false,
                    ErrorCode: "RemoteUriOutsideSource");
            }

            using var client = CreateClient(source);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                mediaUri);
            request.Headers.Range = new RangeHeaderValue(0, 0);

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                var total = response.Content.Headers.ContentRange?.Length;
                return new WebDavMediaProbeResult(
                    true,
                    true,
                    total ?? response.Content.Headers.ContentLength);
            }

            if (response.IsSuccessStatusCode)
            {
                var acceptsRanges =
                    response.Headers.AcceptRanges.Any(value =>
                        string.Equals(
                            value,
                            "bytes",
                            StringComparison.OrdinalIgnoreCase));

                return new WebDavMediaProbeResult(
                    true,
                    acceptsRanges,
                    response.Content.Headers.ContentLength);
            }

            return new WebDavMediaProbeResult(
                false,
                false,
                ErrorCode: MapStatusCode(response.StatusCode),
                Detail: $"{(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WebDavMediaProbeResult(
                false,
                false,
                ErrorCode: "Timeout");
        }
        catch (HttpRequestException exception)
        {
            return new WebDavMediaProbeResult(
                false,
                false,
                ErrorCode: "NetworkError",
                Detail: exception.Message);
        }
        catch (MediaSourceException exception)
        {
            return new WebDavMediaProbeResult(
                false,
                false,
                ErrorCode: exception.ErrorCode,
                Detail: exception.Message);
        }
    }

    private HttpClient CreateClient(MediaSourceDefinition source)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate,
            PreAuthenticate = true
        };

        var credential = _credentialStore.GetWebDav(source);
        if (credential is not null)
        {
            handler.Credentials = new NetworkCredential(
                credential.UserName,
                credential.Password);
        }

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Eizo/0.3.0");
        return client;
    }

    private static HttpRequestMessage CreatePropFindRequest(
        Uri uri,
        int depth)
    {
        const string body =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<d:propfind xmlns:d=\"DAV:\">" +
            "<d:prop>" +
            "<d:resourcetype/>" +
            "<d:getcontentlength/>" +
            "<d:getlastmodified/>" +
            "<d:getetag/>" +
            "</d:prop>" +
            "</d:propfind>";

        var request = new HttpRequestMessage(
            PropFindMethod,
            uri);
        request.Headers.TryAddWithoutValidation(
            "Depth",
            depth.ToString(CultureInfo.InvariantCulture));
        request.Content = new StringContent(
            body,
            Encoding.UTF8,
            "application/xml");
        return request;
    }

    private static XElement? SelectSuccessfulPropertySet(
        XElement responseElement)
    {
        foreach (var propStat in responseElement.Elements(Dav + "propstat"))
        {
            var status = propStat.Element(Dav + "status")?.Value;
            if (status?.Contains(
                    " 200 ",
                    StringComparison.Ordinal) == true)
            {
                return propStat.Element(Dav + "prop");
            }
        }

        return responseElement.Element(Dav + "prop");
    }

    private static Uri? ResolveHref(Uri requestUri, string href)
    {
        try
        {
            return Uri.TryCreate(href, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(requestUri, href);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    private static Uri BuildRequestUri(
        Uri rootUri,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return rootUri;

        var isDirectory =
            relativePath.EndsWith("/", StringComparison.Ordinal);
        var escapedSegments = relativePath
            .Trim('/')
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);

        var escaped = string.Join("/", escapedSegments);
        if (isDirectory)
            escaped += "/";

        return new Uri(rootUri, escaped);
    }

    private static string GetRelativePath(
        Uri rootUri,
        Uri itemUri)
    {
        var rootPath = Uri.UnescapeDataString(rootUri.AbsolutePath);
        if (!rootPath.EndsWith("/", StringComparison.Ordinal))
            rootPath += "/";

        var itemPath = Uri.UnescapeDataString(itemUri.AbsolutePath);
        if (!itemPath.StartsWith(
                rootPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return itemPath[rootPath.Length..]
            .TrimStart('/');
    }

    private static bool IsUriWithinRoot(
        Uri rootUri,
        Uri itemUri)
    {
        if (!string.Equals(
                rootUri.Scheme,
                itemUri.Scheme,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                rootUri.Authority,
                itemUri.Authority,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rootPath = Uri.UnescapeDataString(rootUri.AbsolutePath);
        if (!rootPath.EndsWith("/", StringComparison.Ordinal))
            rootPath += "/";

        var itemPath = Uri.UnescapeDataString(itemUri.AbsolutePath);
        return itemPath.StartsWith(
            rootPath,
            StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                itemPath.TrimEnd('/'),
                rootPath.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameResource(Uri left, Uri right) =>
        string.Equals(
            left.GetLeftPart(UriPartial.Path).TrimEnd('/'),
            right.GetLeftPart(UriPartial.Path).TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);

    private static void ValidateSource(MediaSourceDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Kind != MediaSourceKind.WebDav)
        {
            throw new MediaSourceException(
                "SourceKindMismatch",
                "The media source is not WebDAV.");
        }

        if (string.IsNullOrWhiteSpace(source.RootLocation) ||
            !Uri.TryCreate(
                source.RootLocation,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new MediaSourceException(
                "SourceRootInvalid",
                "The WebDAV source root is not a valid HTTP(S) URI.");
        }
    }

    private static string MapStatusCode(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "AuthenticationFailed",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "NotFound",
            HttpStatusCode.RequestTimeout => "Timeout",
            _ => "HttpError"
        };
}
