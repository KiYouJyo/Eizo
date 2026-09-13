using System.Net;
using Eizo.Bangumi;

namespace Eizo.Models;

internal sealed class BangumiAccountService
{
    private readonly BangumiRepository _repository;
    private readonly BangumiAccountCredentialStore _credentials;
    private BangumiUserProfile? _profile;

    private BangumiAccountService()
        : this(
            BangumiRepository.Default,
            BangumiAccountCredentialStore.Default)
    {
    }

    internal BangumiAccountService(
        BangumiRepository repository,
        BangumiAccountCredentialStore credentials)
    {
        _repository = repository;
        _credentials = credentials;
    }

    public static BangumiAccountService Default { get; } = new();

    public event EventHandler? Changed;

    public bool IsConnected =>
        !string.IsNullOrWhiteSpace(
            _credentials.GetAccessToken());

    public BangumiUserProfile? CachedProfile =>
        _profile;

    public async Task<BangumiUserProfile> ConnectAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var normalizedToken = accessToken.Trim();
        const string bearerPrefix = "Bearer ";
        if (normalizedToken.StartsWith(
                bearerPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            normalizedToken =
                normalizedToken[bearerPrefix.Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(normalizedToken))
            throw new ArgumentException(
                "Bangumi access token is empty.",
                nameof(accessToken));

        var profile = await _repository.GetMyselfAsync(
            normalizedToken,
            cancellationToken);

        _credentials.SaveAccessToken(normalizedToken);
        _profile = profile;
        Changed?.Invoke(this, EventArgs.Empty);
        return profile;
    }

    public async Task<BangumiUserProfile> ConnectOAuthAsync(
        string accessToken, string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        var profile = await _repository.GetMyselfAsync(accessToken, cancellationToken);
        _credentials.SaveTokens(accessToken, refreshToken);
        _profile = profile;
        Changed?.Invoke(this, EventArgs.Empty);
        return profile;
    }

    public async Task<BangumiUserProfile?> GetProfileAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _profile is not null)
            return _profile;

        var token = _credentials.GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            _profile = await _repository.GetMyselfAsync(
                token,
                cancellationToken);
            return _profile;
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Disconnect();
            return null;
        }
    }

    public async Task<BangumiUserCollectionPage?>
        GetFollowingAsync(
            int offset = 0,
            CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(
            forceRefresh: false,
            cancellationToken);
        if (profile is null)
            return null;

        var token = _credentials.GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            return await _repository.GetFollowingAsync(
                token,
                profile.UserName,
                offset,
                cancellationToken);
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Disconnect();
            return null;
        }
    }

    public void Disconnect()
    {
        var hadSession =
            IsConnected ||
            _profile is not null;

        _credentials.RemoveAccessToken();
        _profile = null;

        if (hadSession)
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
