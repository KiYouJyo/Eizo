using Windows.Security.Credentials;

namespace Eizo.Models;

public sealed record MediaCredentialSnapshot(
    string UserName,
    string Password);

public sealed class MediaCredentialStore
{
    private const string WebDavResourcePrefix = "Eizo.WebDav:";

    private readonly PasswordVault _vault = new();

    private MediaCredentialStore()
    {
    }

    public static MediaCredentialStore Default { get; } = new();

    public static string BuildWebDavCredentialKey(string sourceId) =>
        WebDavResourcePrefix + sourceId;

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
