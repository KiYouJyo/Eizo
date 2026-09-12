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
            out _,
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

    internal static bool IsExplicitSpecialSource(
        string? sourceTitle) =>
        TryParseExplicitSpecial(
            sourceTitle,
            out _,
            out _);

    internal static string? ResolveExplicitSpecialLabel(
        string? sourceTitle,
        decimal? fallbackNumber)
    {
        if (!TryParseExplicitSpecial(
                sourceTitle,
                out var kind,
                out var parsedNumber))
        {
            return null;
        }

        var number = parsedNumber ?? fallbackNumber;
        if (number is null)
            return kind;

        var formatted =
            number == decimal.Truncate(number.Value)
                ? decimal.Truncate(number.Value)
                    .ToString(CultureInfo.InvariantCulture)
                : number.Value.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture);

        return $"{kind} {formatted}";
    }

    private static bool TryParseExplicitSpecial(
        string? sourceTitle,
        out string kind,
        out decimal? number)
    {
        kind = string.Empty;
        number = null;

        if (string.IsNullOrWhiteSpace(sourceTitle))
            return false;

        var normalized = sourceTitle
            .Normalize(NormalizationForm.FormKC)
            .Trim();

        var match = ExplicitSpecialRegex.Match(normalized);
        if (!match.Success)
            return false;

        kind = match.Groups["kind"].Value
            .ToUpperInvariant();

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
