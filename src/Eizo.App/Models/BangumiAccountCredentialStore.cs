using Windows.Security.Credentials;

namespace Eizo.Models;

internal sealed class BangumiAccountCredentialStore
{
    private const string ResourceName = "Eizo.Bangumi";
    private const string CredentialUserName = "access-token";

    private readonly PasswordVault _vault = new();

    private BangumiAccountCredentialStore()
    {
    }

    public static BangumiAccountCredentialStore Default { get; } = new();

    public string? GetAccessToken()
    {
        try
        {
            var credential = _vault.Retrieve(
                ResourceName,
                CredentialUserName);
            credential.RetrievePassword();

            var token = credential.Password?.Trim();
            return string.IsNullOrWhiteSpace(token)
                ? null
                : token;
        }
        catch
        {
            return null;
        }
    }

    public void SaveAccessToken(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        RemoveAccessToken();
        _vault.Add(
            new PasswordCredential(
                ResourceName,
                CredentialUserName,
                accessToken.Trim()));
    }

    public void RemoveAccessToken()
    {
        try
        {
            foreach (var credential in
                     _vault.FindAllByResource(ResourceName))
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
