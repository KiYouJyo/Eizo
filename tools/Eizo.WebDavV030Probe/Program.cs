using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Eizo.Models;

namespace Eizo.Models
{
    public static class MediaSourceProviderRegistry
    {
        public static IMediaSourceProvider? Provider { get; set; }

        public static bool TryGet(
            MediaSourceKind kind,
            out IMediaSourceProvider provider)
        {
            if (Provider is not null &&
                Provider.Kind == kind)
            {
                provider = Provider;
                return true;
            }

            provider = null!;
            return false;
        }
    }
}

namespace Eizo.WebDavV030Probe
{
    internal static class Program
    {
        public static async Task<int> Main()
        {
            var localData = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Eizo");
            Directory.CreateDirectory(localData);

            var catalogPath =
                Path.Combine(localData, "catalog.json");
            if (File.Exists(catalogPath))
                File.Delete(catalogPath);

            await using var server =
                new WebDavProbeServer(
                    "alice",
                    "secret");

            var source = new MediaSourceDefinition(
                "webdav-probe",
                MediaSourceKind.WebDav,
                "Probe DAV",
                server.RootUri.AbsoluteUri,
                UserName: "alice",
                CredentialKey: "probe");

            var provider =
                new WebDavMediaSourceProvider(
                    new FixedCredentialProvider(
                        "alice",
                        "secret"));

            MediaSourceProviderRegistry.Provider =
                provider;

            var connection =
                await provider.TestConnectionAsync(
                    source);

            Assert(
                connection.IsAvailable,
                "Authenticated WebDAV connection failed.");

            var rootEntries =
                new List<MediaSourceEntry>();

            await foreach (var entry in
                provider.ListAsync(source))
            {
                rootEntries.Add(entry);
            }

            Assert(
                rootEntries.Count == 2,
                $"Expected 2 root entries, got {rootEntries.Count}.");

            Assert(
                rootEntries.Any(entry =>
                    entry.IsDirectory &&
                    entry.RelativePath ==
                        "Season 1/"),
                "Season 1 collection missing from PROPFIND.");

            Assert(
                rootEntries.Any(entry =>
                    !entry.IsDirectory &&
                    entry.Name ==
                        "root-video.mp4"),
                "Root video missing from PROPFIND.");

            var catalog =
                MediaCatalogStore.Default;

            var scanned =
                await catalog.ScanSourceAsync(
                    source);

            Assert(
                scanned == 2,
                $"Expected 2 scanned videos, got {scanned}.");

            var items =
                catalog.SnapshotForSource(
                    source.Id);

            Assert(
                items.Count == 2,
                "Remote catalog snapshot count mismatch.");

            Assert(
                items.All(item =>
                    item.Location is
                    {
                        Kind:
                            MediaLocationKind.RemoteUri
                    }),
                "WebDAV scan produced a non-remote location.");

            Assert(
                items.Any(item =>
                    item.Location?.SizeBytes >
                    int.MaxValue),
                "Large-file metadata was not preserved.");

            var selectedSource =
                source with
                {
                    Id = "webdav-probe-selected",
                    SelectedPaths =
                        ["Season 1/"]
                };

            var selectedScanned =
                await catalog.ScanSourceAsync(
                    selectedSource);

            Assert(
                selectedScanned == 1,
                $"Expected selected-folder scan to find 1 video, got {selectedScanned}.");

            var selectedItems =
                catalog.SnapshotForSource(
                    selectedSource.Id);

            Assert(
                selectedItems.Count == 1 &&
                selectedItems[0].Location?.Locator
                    .EndsWith(
                        "episode-02.mkv",
                        StringComparison.OrdinalIgnoreCase) == true,
                "Selected-folder scan escaped its configured WebDAV root.");

            var episodeUri =
                new Uri(
                    server.RootUri,
                    "Season%201/episode-02.mkv");

            var probe =
                await provider.ProbeMediaAsync(
                    source,
                    episodeUri);

            Assert(
                probe.IsAvailable,
                "Range probe reported media unavailable.");
            Assert(
                probe.SupportsRanges,
                "Range probe did not detect byte-range support.");
            Assert(
                probe.ContentLength ==
                    WebDavProbeServer.LargeMediaLength,
                "Range probe lost the large content length.");

            var missing =
                await provider.ProbeMediaAsync(
                    source,
                    new Uri(
                        server.RootUri,
                        "missing.mkv"));

            Assert(
                !missing.IsAvailable &&
                missing.ErrorCode == "NotFound",
                "404 media probe was not classified as NotFound.");

            var wrongProvider =
                new WebDavMediaSourceProvider(
                    new FixedCredentialProvider(
                        "alice",
                        "wrong"));

            var unauthorized =
                await wrongProvider
                    .TestConnectionAsync(
                        source);

            Assert(
                !unauthorized.IsAvailable &&
                unauthorized.ErrorCode ==
                    "AuthenticationFailed",
                "401 WebDAV connection was not classified as AuthenticationFailed.");

            var outside =
                await provider.ProbeMediaAsync(
                    source,
                    new Uri(
                        server.OriginUri,
                        "outside/video.mkv"));

            Assert(
                !outside.IsAvailable &&
                outside.ErrorCode ==
                    "RemoteUriOutsideSource",
                "Out-of-root media URI was not rejected.");

            Assert(
                server.RangeRequestCount > 0,
                "The media probe did not issue a byte-range request.");
            Assert(
                server.UnauthorizedRequestCount > 0,
                "The authentication failure path was not exercised.");

            Console.WriteLine(
                "Eizo v0.3.0 WebDAV runtime probe PASS. " +
                $"Catalog={items.Count}; " +
                $"LargeBytes={probe.ContentLength}; " +
                $"RangeRequests={server.RangeRequestCount}; " +
                $"UnauthorizedRequests={server.UnauthorizedRequestCount}");

            return 0;
        }

        private static void Assert(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(
                    message);
        }

        private sealed class FixedCredentialProvider(
            string userName,
            string password)
            : IMediaCredentialProvider
        {
            public MediaCredentialSnapshot? GetWebDav(
                MediaSourceDefinition source) =>
                new(userName, password);
        }

        private sealed class WebDavProbeServer
            : IAsyncDisposable
        {
            public const long LargeMediaLength =
                5L * 1024L * 1024L * 1024L;

            private readonly TcpListener _listener =
                new(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource _shutdown =
                new();
            private readonly string _expectedAuthorization;
            private Task? _acceptLoop;
            private int _rangeRequestCount;
            private int _unauthorizedRequestCount;

            public WebDavProbeServer(
                string userName,
                string password)
            {
                _expectedAuthorization =
                    "Basic " +
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(
                            userName + ":" + password));

                _listener.Start();

                var endpoint =
                    (IPEndPoint)_listener.LocalEndpoint;

                OriginUri = new Uri(
                    $"http://127.0.0.1:{endpoint.Port}/");
                RootUri = new Uri(
                    OriginUri,
                    "dav/");

                _acceptLoop = AcceptLoopAsync(
                    _shutdown.Token);
            }

            public Uri OriginUri { get; }

            public Uri RootUri { get; }

            public int RangeRequestCount =>
                Volatile.Read(
                    ref _rangeRequestCount);

            public int UnauthorizedRequestCount =>
                Volatile.Read(
                    ref _unauthorizedRequestCount);

            public async ValueTask DisposeAsync()
            {
                _shutdown.Cancel();
                _listener.Stop();

                if (_acceptLoop is not null)
                {
                    try
                    {
                        await _acceptLoop;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (SocketException)
                    {
                    }
                }

                _shutdown.Dispose();
            }

            private async Task AcceptLoopAsync(
                CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        client =
                            await _listener
                                .AcceptTcpClientAsync(
                                    cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    _ = Task.Run(
                        () => HandleClientAsync(
                            client,
                            cancellationToken),
                        cancellationToken);
                }
            }

            private async Task HandleClientAsync(
                TcpClient client,
                CancellationToken cancellationToken)
            {
                using (client)
                await using (var stream =
                    client.GetStream())
                {
                    var request =
                        await ReadRequestAsync(
                            stream,
                            cancellationToken);

                    if (request is null)
                        return;

                    if (!request.Headers.TryGetValue(
                            "Authorization",
                            out var authorization) ||
                        !string.Equals(
                            authorization,
                            _expectedAuthorization,
                            StringComparison.Ordinal))
                    {
                        Interlocked.Increment(
                            ref _unauthorizedRequestCount);

                        await WriteResponseAsync(
                            stream,
                            "401 Unauthorized",
                            [
                                "WWW-Authenticate: Basic realm=\"EizoWebDavProbe\"",
                                "Content-Length: 0"
                            ],
                            null,
                            cancellationToken);
                        return;
                    }

                    var decodedPath =
                        Uri.UnescapeDataString(
                            request.Path.Split(
                                '?',
                                2)[0]);

                    if (string.Equals(
                            request.Method,
                            "PROPFIND",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        await HandlePropFindAsync(
                            stream,
                            decodedPath,
                            request.Headers,
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(
                            request.Method,
                            "GET",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            request.Method,
                            "HEAD",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleMediaAsync(
                            stream,
                            request,
                            decodedPath,
                            cancellationToken);
                        return;
                    }

                    await WriteResponseAsync(
                        stream,
                        "405 Method Not Allowed",
                        ["Content-Length: 0"],
                        null,
                        cancellationToken);
                }
            }

            private async Task HandlePropFindAsync(
                NetworkStream stream,
                string path,
                IReadOnlyDictionary<string, string> headers,
                CancellationToken cancellationToken)
            {
                var depth =
                    headers.TryGetValue(
                        "Depth",
                        out var depthValue)
                        ? depthValue
                        : "0";

                string? xml = path switch
                {
                    "/dav/" when depth == "0" =>
                        MultiStatus(
                            Collection("/dav/")),

                    "/dav/" =>
                        MultiStatus(
                            Collection("/dav/"),
                            Collection("/dav/Season%201/"),
                            FileItem(
                                "/dav/root-video.mp4",
                                1_234_567_890L)),

                    "/dav/Season 1/" =>
                        MultiStatus(
                            Collection("/dav/Season%201/"),
                            FileItem(
                                "/dav/Season%201/episode-02.mkv",
                                LargeMediaLength)),

                    _ => null
                };

                if (xml is null)
                {
                    await WriteResponseAsync(
                        stream,
                        "404 Not Found",
                        ["Content-Length: 0"],
                        null,
                        cancellationToken);
                    return;
                }

                var body =
                    Encoding.UTF8.GetBytes(
                        xml);

                await WriteResponseAsync(
                    stream,
                    "207 Multi-Status",
                    [
                        "Content-Type: application/xml; charset=utf-8",
                        $"Content-Length: {body.Length}"
                    ],
                    body,
                    cancellationToken);
            }

            private async Task HandleMediaAsync(
                NetworkStream stream,
                Request request,
                string path,
                CancellationToken cancellationToken)
            {
                long totalLength = path switch
                {
                    "/dav/root-video.mp4" =>
                        1_234_567_890L,
                    "/dav/Season 1/episode-02.mkv" =>
                        LargeMediaLength,
                    _ => -1
                };

                if (totalLength < 0)
                {
                    await WriteResponseAsync(
                        stream,
                        "404 Not Found",
                        ["Content-Length: 0"],
                        null,
                        cancellationToken);
                    return;
                }

                if (request.Headers.TryGetValue(
                        "Range",
                        out var range) &&
                    range.StartsWith(
                        "bytes=0-0",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(
                        ref _rangeRequestCount);

                    await WriteResponseAsync(
                        stream,
                        "206 Partial Content",
                        [
                            "Accept-Ranges: bytes",
                            $"Content-Range: bytes 0-0/{totalLength}",
                            "Content-Length: 1",
                            "Content-Type: application/octet-stream"
                        ],
                        request.Method.Equals(
                            "HEAD",
                            StringComparison.OrdinalIgnoreCase)
                            ? null
                            : [0],
                        cancellationToken);
                    return;
                }

                await WriteResponseAsync(
                    stream,
                    "200 OK",
                    [
                        "Accept-Ranges: bytes",
                        $"Content-Length: {totalLength}",
                        "Content-Type: application/octet-stream"
                    ],
                    null,
                    cancellationToken);
            }

            private static string MultiStatus(
                params string[] responses) =>
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<d:multistatus xmlns:d=\"DAV:\">" +
                string.Concat(responses) +
                "</d:multistatus>";

            private static string Collection(
                string href) =>
                "<d:response>" +
                $"<d:href>{href}</d:href>" +
                "<d:propstat><d:prop>" +
                "<d:resourcetype><d:collection/></d:resourcetype>" +
                "<d:getlastmodified>Tue, 09 Sep 2026 09:00:00 GMT</d:getlastmodified>" +
                "</d:prop>" +
                "<d:status>HTTP/1.1 200 OK</d:status>" +
                "</d:propstat>" +
                "</d:response>";

            private static string FileItem(
                string href,
                long length) =>
                "<d:response>" +
                $"<d:href>{href}</d:href>" +
                "<d:propstat><d:prop>" +
                "<d:resourcetype/>" +
                $"<d:getcontentlength>{length.ToString(CultureInfo.InvariantCulture)}</d:getcontentlength>" +
                "<d:getlastmodified>Tue, 09 Sep 2026 09:00:00 GMT</d:getlastmodified>" +
                "<d:getetag>\"probe-etag\"</d:getetag>" +
                "</d:prop>" +
                "<d:status>HTTP/1.1 200 OK</d:status>" +
                "</d:propstat>" +
                "</d:response>";

            private static async Task<Request?> ReadRequestAsync(
                NetworkStream stream,
                CancellationToken cancellationToken)
            {
                using var reader =
                    new StreamReader(
                        stream,
                        Encoding.ASCII,
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: 4096,
                        leaveOpen: true);

                var requestLine =
                    await reader.ReadLineAsync(
                        cancellationToken);

                if (string.IsNullOrWhiteSpace(
                        requestLine))
                {
                    return null;
                }

                var parts =
                    requestLine.Split(
                        ' ',
                        3,
                        StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2)
                    return null;

                var headers =
                    new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);

                while (true)
                {
                    var line =
                        await reader.ReadLineAsync(
                            cancellationToken);

                    if (string.IsNullOrEmpty(line))
                        break;

                    var separator =
                        line.IndexOf(':');

                    if (separator <= 0)
                        continue;

                    headers[
                        line[..separator].Trim()] =
                        line[(separator + 1)..].Trim();
                }

                return new Request(
                    parts[0],
                    parts[1],
                    headers);
            }

            private static async Task WriteResponseAsync(
                NetworkStream stream,
                string status,
                IEnumerable<string> headers,
                byte[]? body,
                CancellationToken cancellationToken)
            {
                var headerText =
                    "HTTP/1.1 " + status + "\r\n" +
                    string.Join(
                        "\r\n",
                        headers) +
                    "\r\nConnection: close\r\n\r\n";

                var headerBytes =
                    Encoding.ASCII.GetBytes(
                        headerText);

                await stream.WriteAsync(
                    headerBytes,
                    cancellationToken);

                if (body is { Length: > 0 })
                {
                    await stream.WriteAsync(
                        body,
                        cancellationToken);
                }

                await stream.FlushAsync(
                    cancellationToken);
            }

            private sealed record Request(
                string Method,
                string Path,
                IReadOnlyDictionary<string, string> Headers);
        }
    }
}
