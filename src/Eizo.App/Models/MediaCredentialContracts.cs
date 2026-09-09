namespace Eizo.Models;

public sealed record MediaCredentialSnapshot(
    string UserName,
    string Password);

public interface IMediaCredentialProvider
{
    MediaCredentialSnapshot? GetWebDav(
        MediaSourceDefinition source);
}
