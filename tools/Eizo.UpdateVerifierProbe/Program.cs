namespace Eizo;

internal static class Program
{
    private const string ExpectedSubject = "CN=AppPublisher";
    private const string ExpectedThumbprint = "BD85AD77A651C86CA01A480C8E9BC64952993F98";

    public static int Main(string[] args)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("Usage: Eizo.UpdateVerifierProbe <signed-msixbundle>");
            return 2;
        }

        var path = Path.GetFullPath(args[0]);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Bundle not found: {path}");
            return 3;
        }

        var result = new MsixBundleSignatureVerifier().Verify(path);
        Console.WriteLine(
            $"Valid={result.IsValid}; Code={result.FailureCode}; Subject={result.SignerSubject}; Thumbprint={result.SignerThumbprint}; HRESULT={result.HResult}");

        if (!result.IsValid)
            return 10;

        if (!string.Equals(result.SignerSubject, ExpectedSubject, StringComparison.Ordinal))
            return 11;

        if (!string.Equals(result.SignerThumbprint, ExpectedThumbprint, StringComparison.OrdinalIgnoreCase))
            return 12;

        return 0;
    }
}
