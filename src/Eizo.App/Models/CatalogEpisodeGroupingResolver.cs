using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Eizo.Models;

internal readonly record struct CatalogEpisodeGroupingIdentity(
    int SeasonNumber,
    decimal? EpisodeNumber,
    bool IsSpecial);

internal static class CatalogEpisodeGroupingResolver
{
    private const RegexOptions Options =
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly TimeSpan RegexTimeout =
        TimeSpan.FromMilliseconds(50);

    private static readonly Regex ExplicitSpecialRegex = new(
        @"(?<![\p{L}\p{N}])(?<kind>OVA|OAD|ONA|SP|SPECIALS?|NCOP|NCED)(?:\s*[-_. ]?\s*(?<number>\d{1,3}(?:\.\d+)?))?(?=\s*(?:$|\[|\(|【|\]|\)|】))",
        Options,
        RegexTimeout);

    internal static CatalogEpisodeGroupingIdentity Resolve(
        string? sourceTitle,
        string? mediaKind,
        string? specialKind,
        int? seasonNumber,
        decimal? episodeNumber,
        decimal? specialNumber)
    {
        var explicitSpecial = TryParseExplicitSpecial(
            sourceTitle,
            out var explicitSpecialNumber);

        var recognitionSpecial =
            string.Equals(
                mediaKind,
                "Special",
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(specialKind) &&
            !string.Equals(
                specialKind,
                "None",
                StringComparison.OrdinalIgnoreCase) ||
            specialNumber is not null;

        if (explicitSpecial || recognitionSpecial)
        {
            // Explicit OVA/OAD/ONA/SP markers are file-level evidence and must
            // win over stale persisted Recognition snapshots. Specials use
            // season 0 in Eizo so they can never collapse into S01E01.
            var resolvedSeason = explicitSpecial
                ? 0
                : seasonNumber ?? 0;

            var resolvedNumber =
                specialNumber ??
                explicitSpecialNumber ??
                episodeNumber;

            return new CatalogEpisodeGroupingIdentity(
                resolvedSeason,
                resolvedNumber,
                IsSpecial: true);
        }

        return new CatalogEpisodeGroupingIdentity(
            seasonNumber ?? 1,
            episodeNumber,
            IsSpecial: false);
    }

    private static bool TryParseExplicitSpecial(
        string? sourceTitle,
        out decimal? number)
    {
        number = null;

        if (string.IsNullOrWhiteSpace(sourceTitle))
            return false;

        var normalized = sourceTitle
            .Normalize(NormalizationForm.FormKC)
            .Trim();

        var match = ExplicitSpecialRegex.Match(normalized);
        if (!match.Success)
            return false;

        var value = match.Groups["number"].Value;
        if (!string.IsNullOrWhiteSpace(value) &&
            decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            number = parsed;
        }

        return true;
    }
}
