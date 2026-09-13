using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Windows.System;

namespace Eizo.Models;

internal sealed class BangumiOAuthService
{
    private static readonly Uri RelayBase = new("https://eizo-bangumi-auth.x2425618950.workers.dev/");
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly BangumiAccountCredentialStore _credentials = BangumiAccountCredentialStore.Default;

    public static BangumiOAuthService Default { get; } = new();

    public async Task StartAsync()
    {
        var state = RandomValue();
        var verifier = RandomValue();
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64Url(hash);
        _credentials.SavePendingLogin(state, verifier);
        var url = new Uri(RelayBase, $"login?state={state}&challenge={challenge}");
        if (!await Launcher.LaunchUriAsync(url))
        {
            _credentials.RemovePendingLogin();
            throw new InvalidOperationException("Could not open the browser.");
        }
    }

    public async Task<bool> CompleteAsync(Uri uri)
    {
        var removePending = false;
        try
        {
            if (uri.Scheme != "eizo" || uri.Host != "bangumi-auth") return false;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length != 2 || !values.TryAdd(pair[0], Uri.UnescapeDataString(pair[1]))) return false;
            }
            if (!values.TryGetValue("state", out var state) ||
                !values.TryGetValue("ticket", out var ticket) ||
                !IsOpaque(state) || !IsOpaque(ticket)) return false;
            var verifier = _credentials.GetPendingVerifier(state);
            if (verifier is null) return false;
            removePending = true;
            using var response = await Client.PostAsJsonAsync(new Uri(RelayBase, "claim"),
                new { state, ticket, verifier });
            if (!response.IsSuccessStatusCode) return false;
            var tokens = await response.Content.ReadFromJsonAsync<TokenResult>();
            if (string.IsNullOrWhiteSpace(tokens?.AccessToken)) return false;
            await BangumiAccountService.Default.ConnectOAuthAsync(tokens.AccessToken, tokens.RefreshToken);
            return true;
        }
        catch { return false; }
        finally { if (removePending) _credentials.RemovePendingLogin(); }
    }

    private static bool IsOpaque(string value) => value.Length is >= 32 and <= 128 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private static string RandomValue() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TokenResult
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }
}
