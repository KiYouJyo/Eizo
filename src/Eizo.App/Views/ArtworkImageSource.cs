using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eizo.Views;

internal static class ArtworkImageSource
{
    public static ImageSource? Create(
        string? value,
        int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var allowed =
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, "ms-appx", StringComparison.OrdinalIgnoreCase);

        if (!allowed)
            return null;

        try
        {
            if (uri.AbsolutePath.EndsWith(
                    ".svg",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new SvgImageSource(uri);
            }

            return new BitmapImage
            {
                UriSource = uri,
                DecodePixelWidth = decodePixelWidth,
            };
        }
        catch
        {
            return null;
        }
    }
}
