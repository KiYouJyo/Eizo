using Windows.Graphics;

namespace Eizo;

internal sealed class WindowPlacementService
{
    public const int MinimumWidth = 320;
    public const int MinimumHeight = 240;
    private const int MaximumSavedDimension = 16384;
    private const int DefaultWidth = 1440;
    private const int DefaultHeight = 960;

    public WindowPlacement Load(SizeInt32 workArea)
    {
        var settings = AppSettingsStore.Current;
        var placement = IsValidSavedSize(settings.LastNormalWindowWidth, settings.LastNormalWindowHeight)
            ? new WindowPlacement(
                settings.LastNormalWindowWidth!.Value,
                settings.LastNormalWindowHeight!.Value,
                settings.WasWindowMaximized)
            : CreateDefault(workArea);

        return ClampToWorkArea(placement, workArea);
    }

    public void Save(SizeInt32 lastNormalSize, bool wasMaximized)
    {
        if (!IsValidSavedSize(lastNormalSize.Width, lastNormalSize.Height)) return;

        AppSettingsStore.Update(settings => settings with
        {
            LastNormalWindowWidth = lastNormalSize.Width,
            LastNormalWindowHeight = lastNormalSize.Height,
            WasWindowMaximized = wasMaximized
        });
    }

    public static WindowPlacement CreateDefault(SizeInt32 workArea) =>
        ClampToWorkArea(
            new WindowPlacement(DefaultWidth, DefaultHeight, false),
            workArea);

    public static WindowPlacement ClampToWorkArea(WindowPlacement placement, SizeInt32 workArea)
    {
        var maxWidth = Math.Max(1, workArea.Width);
        var maxHeight = Math.Max(1, workArea.Height);

        return placement with
        {
            Width = Math.Clamp(
                placement.Width,
                Math.Min(MinimumWidth, maxWidth),
                maxWidth),
            Height = Math.Clamp(
                placement.Height,
                Math.Min(MinimumHeight, maxHeight),
                maxHeight)
        };
    }

    private static bool IsValidSavedSize(int? width, int? height) =>
        width is >= MinimumWidth and <= MaximumSavedDimension &&
        height is >= MinimumHeight and <= MaximumSavedDimension;
}

internal sealed record WindowPlacement(int Width, int Height, bool WasMaximized);
