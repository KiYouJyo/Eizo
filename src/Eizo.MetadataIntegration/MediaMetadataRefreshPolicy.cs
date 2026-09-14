namespace Eizo.MetadataIntegration;

public static class MediaMetadataRefreshPolicy
{
    public const string Fresh = "Fresh";
    public const string Reused = "Reused";
    public const string RetainedLastKnownGood =
        "RetainedLastKnownGood";
    public const string Failed = "Failed";

    public static MediaMetadataSnapshot? Select(
        MediaMetadataSnapshot? existing,
        MediaMetadataSnapshot? candidate,
        bool forceRefresh,
        DateTimeOffset attemptedAtUtc)
    {
        if (candidate is { IsResolved: true })
        {
            return candidate with
            {
                RefreshState = Fresh,
                LastRefreshAttemptUtc = attemptedAtUtc,
                RefreshErrors = candidate.Errors.ToList(),
            };
        }

        if (existing is { IsResolved: true } &&
            ShouldRetainExisting(
                candidate,
                forceRefresh))
        {
            return existing with
            {
                RefreshState = RetainedLastKnownGood,
                LastRefreshAttemptUtc = attemptedAtUtc,
                RefreshErrors =
                    candidate?.Errors.ToList() ?? [],
            };
        }

        if (candidate is null)
            return existing;

        return candidate with
        {
            RefreshState = Failed,
            LastRefreshAttemptUtc = attemptedAtUtc,
            RefreshErrors = candidate.Errors.ToList(),
        };
    }

    public static MediaMetadataSnapshot MarkReused(
        MediaMetadataSnapshot metadata) =>
        metadata with
        {
            RefreshState = Reused,
        };

    private static bool ShouldRetainExisting(
        MediaMetadataSnapshot? candidate,
        bool forceRefresh)
    {
        if (!forceRefresh)
            return true;

        if (candidate is null)
            return true;

        return IsTransportFailure(candidate);
    }

    public static bool IsTransportFailure(
        MediaMetadataSnapshot metadata) =>
        metadata.Status != MediaMetadataStatus.Resolved &&
        metadata.Errors.Any(static error =>
            string.Equals(
                error.ErrorType,
                nameof(HttpRequestException),
                StringComparison.Ordinal) ||
            string.Equals(
                error.ErrorType,
                nameof(TaskCanceledException),
                StringComparison.Ordinal) ||
            error.Message.Contains(
                "429",
                StringComparison.OrdinalIgnoreCase) ||
            error.Message.Contains(
                "503",
                StringComparison.OrdinalIgnoreCase) ||
            error.Message.Contains(
                "timed out",
                StringComparison.OrdinalIgnoreCase) ||
            error.Message.Contains(
                "temporarily unavailable",
                StringComparison.OrdinalIgnoreCase));
}
