using System.Globalization;

namespace Eizo.MetadataIntegration;

public sealed record MediaMetadataMatchCandidate(
    string Provider,
    string ProviderSubjectId,
    string SubjectKind,
    string PrimaryTitle,
    string? OriginalTitle,
    int? Year,
    string ContentKind,
    double Score)
{
    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(OriginalTitle) ||
        string.Equals(
            PrimaryTitle,
            OriginalTitle,
            StringComparison.CurrentCultureIgnoreCase)
            ? PrimaryTitle
            : $"{PrimaryTitle} / {OriginalTitle}";

    public string DisplayMeta
    {
        get
        {
            var parts = new List<string>
            {
                Provider.ToUpperInvariant(),
            };

            if (Year is { } year)
            {
                parts.Add(
                    year.ToString(
                        CultureInfo.CurrentCulture));
            }

            if (!string.IsNullOrWhiteSpace(SubjectKind))
                parts.Add(SubjectKind);

            if (!string.IsNullOrWhiteSpace(ContentKind) &&
                !string.Equals(
                    ContentKind,
                    "Unknown",
                    StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(ContentKind);
            }

            parts.Add(
                ProviderSubjectId);

            return string.Join(" · ", parts);
        }
    }
}
