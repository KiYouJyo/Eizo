using Eizo;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Eizo.PlaybackSupport;

internal static class SubtitleStyleProfile
{
    internal const string FontFamilyName = "Microsoft YaHei UI";
    internal const double FontSize = 20d;

    // LibVLC interprets relative font size as a divisor of the decoded video height.
    // A value around 32 keeps embedded text subtitles visually close to Eizo's
    // 20-DIP WinUI overlay across 720p/1080p/4K sources instead of inheriting the
    // much larger VLC default/source style.
    private const int LibVlcRelativeFontSize = 32;

    internal static void ApplyTo(TextBlock textBlock)
    {
        ArgumentNullException.ThrowIfNull(textBlock);

        textBlock.FontFamily = new FontFamily(FontFamilyName);
        textBlock.FontSize = FontSize;
        textBlock.Foreground = new SolidColorBrush(Colors.White);
    }

    internal static IReadOnlyList<string> CreateLibVlcArguments(
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var backgroundOpacity = (int)Math.Round(
            Math.Clamp(
                settings.PrimarySubtitleBackgroundOpacity,
                0d,
                100d) * 255d / 100d,
            MidpointRounding.AwayFromZero);

        return
        [
            $"--freetype-font={FontFamilyName}",
            "--freetype-fontsize=0",
            $"--freetype-rel-fontsize={LibVlcRelativeFontSize}",
            "--freetype-color=16777215",
            "--freetype-opacity=255",
            "--freetype-background-color=0",
            $"--freetype-background-opacity={backgroundOpacity}",
            "--freetype-outline-thickness=0",
            "--freetype-shadow-opacity=0"
        ];
    }
}
