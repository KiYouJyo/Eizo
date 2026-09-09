using System.Net;
using System.Net.Sockets;
using System.Text;
using Eizo.Playback;
using Eizo.Playback.Backends.LibVLC;

await using var server = new AuthenticatedWaveServer(
    "alice",
    "secret",
    CreateWavePayload(seconds: 6));

await using var engine = new LibVlcPlaybackEngine();

var source = PlaybackSource.FromUri(
    server.MediaUri,
    "authenticated-range.wav",
    new PlaybackNetworkAccess(
        "alice",
        "secret"));

var cancellationToken =
    new CancellationTokenSource(
        TimeSpan.FromSeconds(25)).Token;

await engine.OpenAsync(
    source,
    cancellationToken);

await engine.PlayAsync(
    cancellationToken);

await WaitUntilAsync(
    () =>
        engine.State is PlaybackState.Playing
            or PlaybackState.Buffering,
    TimeSpan.FromSeconds(10),
    cancellationToken);

await WaitUntilAsync(
    () => engine.Duration > TimeSpan.FromSeconds(4),
    TimeSpan.FromSeconds(10),
    cancellationToken);

await WaitUntilAsync(
    () => engine.Position > TimeSpan.FromMilliseconds(150),
    TimeSpan.FromSeconds(10),
    cancellationToken);

await engine.SeekAsync(
    TimeSpan.FromSeconds(3),
    cancellationToken);

await WaitUntilAsync(
    () => engine.Position >= TimeSpan.FromSeconds(2.5),
    TimeSpan.FromSeconds(5),
    cancellationToken);

if (engine.State == PlaybackState.Failed)
    throw new InvalidOperationException("Playback engine entered Failed state.");

if (server.AuthorizedRequestCount < 2)
{
    throw new InvalidOperationException(
        $"Expected authenticated HTTP requests, got {server.AuthorizedRequestCount}.");
}

if (server.RangeRequestCount < 2)
{
    throw new InvalidOperationException(
        $"Expected multiple byte-range requests including seek, got {server.RangeRequestCount}.");
}

Console.WriteLine(
    "Authenticated Eizo.Playback probe PASS. " +
    $"State={engine.State}; " +
    $"Duration={engine.Duration}; " +
    $"Position={engine.Position}; " +
    $"AuthorizedRequests={server.AuthorizedRequestCount}; " +
    $"RangeRequests={server.RangeRequestCount}; " +
    $"UnauthorizedChallenges={server.UnauthorizedRequestCount}");

return;

static async Task WaitUntilAsync(
    Func<bool> predicate,
    TimeSpan timeout,
    CancellationToken cancellationToken)
{
    var deadline = DateTime.UtcNow + timeout;

    while (!predicate())
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (DateTime.UtcNow >= deadline)
        {
            throw new TimeoutException(
                "Authenticated playback condition was not reached.");
        }

        await Task.Delay(
            50,
            cancellationToken);
    }
}

static byte[] CreateWavePayload(int seconds)
{
    const int sampleRate = 44_100;
    const short channels = 1;
    const short bitsPerSample = 16;

    var sampleCount = sampleRate * seconds;
    var dataLength =
        sampleCount *
        channels *
        (bitsPerSample / 8);

    using var memory =
        new MemoryStream(
            44 + dataLength);
    using var writer =
        new BinaryWriter(
            memory,
            Encoding.ASCII,
            leaveOpen: true);

    writer.Write(
        Encoding.ASCII.GetBytes("RIFF"));
    writer.Write(36 + dataLength);
    writer.Write(
        Encoding.ASCII.GetBytes("WAVE"));
    writer.Write(
        Encoding.ASCII.GetBytes("fmt "));
    writer.Write(16);
    writer.Write((short)1);
    writer.Write(channels);
    writer.Write(sampleRate);
    writer.Write(
        sampleRate *
        channels *
        (bitsPerSample / 8));
    writer.Write(
        (short)(
            channels *
            (bitsPerSample / 8)));
    writer.Write(bitsPerSample);
    writer.Write(
        Encoding.ASCII.GetBytes("data"));
    writer.Write(dataLength);

    for (var i = 0; i < sampleCount; i++)
    {
        var sample =
            (short)(
                Math.Sin(
                    2d *
                    Math.PI *
                    440d *
                    i /
                    sampleRate) *
                short.MaxValue *
                0.08d);

        writer.Write(sample);
    }

    writer.Flush();
    return memory.ToArray();
}

sealed class AuthenticatedWaveServer
    : IAsyncDisposable
{
    private readonly TcpListener _listener =
        new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _shutdown =
        new();
    private readonly string _expectedAuthorization;
    private readonly byte[] _payload;
    private readonly Task _acceptLoop;

    private int _authorizedRequestCount;
    private int _unauthorizedRequestCount;
    private int _rangeRequestCount;

    public AuthenticatedWaveServer(
        string userName,
        string password,
        byte[] payload)
    {
        _payload = payload;
        _expectedAuthorization =
            "Basic " +
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    userName + ":" + password));

        _listener.Start();

        var endpoint =
            (IPEndPoint)_listener.LocalEndpoint;

        MediaUri = new Uri(
            $"http://127.0.0.1:{endpoint.Port}/media.wav");

        _acceptLoop =
            AcceptLoopAsync(
                _shutdown.Token);
    }

    public Uri MediaUri { get; }

    public int AuthorizedRequestCount =>
        Volatile.Read(
            ref _authorizedRequestCount);

    public int UnauthorizedRequestCount =>
        Volatile.Read(
            ref _unauthorizedRequestCount);

    public int RangeRequestCount =>
        Volatile.Read(
            ref _rangeRequestCount);

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _listener.Stop();

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
                        "WWW-Authenticate: Basic realm=\"EizoAuthPlaybackProbe\"",
                        "Content-Length: 0"
                    ],
                    null,
                    cancellationToken);
                return;
            }

            Interlocked.Increment(
                ref _authorizedRequestCount);

            if (!string.Equals(
                    request.Path,
                    "/media.wav",
                    StringComparison.Ordinal))
            {
                await WriteResponseAsync(
                    stream,
                    "404 Not Found",
                    ["Content-Length: 0"],
                    null,
                    cancellationToken);
                return;
            }

            var start = 0;
            var end = _payload.Length - 1;
            var partial = false;

            if (request.Headers.TryGetValue(
                    "Range",
                    out var range) &&
                TryParseRange(
                    range,
                    _payload.Length,
                    out var requestedStart,
                    out var requestedEnd))
            {
                start = requestedStart;
                end = requestedEnd;
                partial = true;

                Interlocked.Increment(
                    ref _rangeRequestCount);
            }

            var length = end - start + 1;
            var headers = new List<string>
            {
                "Content-Type: audio/wav",
                "Accept-Ranges: bytes",
                $"Content-Length: {length}"
            };

            if (partial)
            {
                headers.Add(
                    $"Content-Range: bytes {start}-{end}/{_payload.Length}");
            }

            var body = _payload
                .AsSpan(start, length)
                .ToArray();

            await WriteResponseAsync(
                stream,
                partial
                    ? "206 Partial Content"
                    : "200 OK",
                headers,
                body,
                cancellationToken);
        }
    }

    private static bool TryParseRange(
        string value,
        int totalLength,
        out int start,
        out int end)
    {
        start = 0;
        end = totalLength - 1;

        if (!value.StartsWith(
                "bytes=",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var range = value[6..]
            .Split(',', 2)[0]
            .Trim();

        var dash = range.IndexOf('-');
        if (dash < 0 ||
            !int.TryParse(
                range[..dash],
                out start))
        {
            return false;
        }

        if (dash < range.Length - 1 &&
            int.TryParse(
                range[(dash + 1)..],
                out var parsedEnd))
        {
            end = Math.Min(
                parsedEnd,
                totalLength - 1);
        }

        start = Math.Clamp(
            start,
            0,
            totalLength - 1);
        end = Math.Clamp(
            end,
            start,
            totalLength - 1);

        return true;
    }

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
