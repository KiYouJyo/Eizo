using Windows.Security.Credentials;

namespace Eizo.Models;

public sealed class MediaCredentialStore : IMediaCredentialProvider
{
    private const string WebDavResourcePrefix = "Eizo.WebDav:";
    private const string TmdbResource = "Eizo.Metadata:TMDB";
    private const string TmdbUserName = "read-access-token";

    private readonly PasswordVault _vault = new();

    private MediaCredentialStore()
    {
    }

    public static MediaCredentialStore Default { get; } = new();

    public static string BuildWebDavCredentialKey(string sourceId) =>
        WebDavResourcePrefix + sourceId;

    public bool HasTmdbReadAccessToken =>
        !string.IsNullOrWhiteSpace(
            GetTmdbReadAccessToken());

    public string? GetTmdbReadAccessToken()
    {
        try
        {
            var credential = _vault.Retrieve(
                TmdbResource,
                TmdbUserName);
            credential.RetrievePassword();
            return string.IsNullOrWhiteSpace(
                    credential.Password)
                ? null
                : credential.Password.Trim();
        }
        catch
        {
            return null;
        }
    }

    public void SaveTmdbReadAccessToken(
        string? token)
    {
        RemoveTmdbReadAccessToken();

        var normalized = token?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        _vault.Add(
            new PasswordCredential(
                TmdbResource,
                TmdbUserName,
                normalized));
    }

    public void RemoveTmdbReadAccessToken()
    {
        try
        {
            foreach (var credential in
                     _vault.FindAllByResource(
                         TmdbResource))
            {
                try
                {
                    _vault.Remove(credential);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    public string? SaveWebDav(
        string sourceId,
        string? userName,
        string? password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        var normalizedUserName = userName?.Trim() ?? string.Empty;
        var normalizedPassword = password ?? string.Empty;

        RemoveWebDav(sourceId);

        if (string.IsNullOrEmpty(normalizedUserName) &&
            string.IsNullOrEmpty(normalizedPassword))
        {
            return null;
        }

        var resource = BuildWebDavCredentialKey(sourceId);
        _vault.Add(
            new PasswordCredential(
                resource,
                normalizedUserName,
                normalizedPassword));

        return resource;
    }

    public MediaCredentialSnapshot? GetWebDav(
        MediaSourceDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Kind != MediaSourceKind.WebDav ||
            string.IsNullOrWhiteSpace(source.CredentialKey))
        {
            return null;
        }

        try
        {
            var credential = _vault.Retrieve(
                source.CredentialKey,
                source.UserName ?? string.Empty);
            credential.RetrievePassword();

            return new MediaCredentialSnapshot(
                credential.UserName ?? string.Empty,
                credential.Password ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    public void RemoveWebDav(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        var resource = BuildWebDavCredentialKey(sourceId);

        try
        {
            foreach (var credential in _vault.FindAllByResource(resource))
            {
                try
                {
                    _vault.Remove(credential);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }
}
