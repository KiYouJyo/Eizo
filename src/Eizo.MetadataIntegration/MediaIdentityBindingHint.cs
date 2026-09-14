namespace Eizo.MetadataIntegration;

public sealed record MediaIdentityBindingHint(
    string EizoMediaId,
    string? PrimaryProvider,
    IReadOnlyDictionary<string, string> ExternalIds,
    bool IsManual);
