using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Globalization;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class DesktopPageBackgroundFactory
{
    private DesktopBackground? background;
    private DesktopPageBackground? pageBackground;

    public DesktopPageBackground Create(DesktopBackground background)
    {
        if (this.background == background && pageBackground is not null)
        {
            return pageBackground;
        }

        this.background = background;
        pageBackground = !string.IsNullOrWhiteSpace(background.Wallpaper)
            ? new DesktopPageBackground(new BitmapImage(new Uri(background.Wallpaper, UriKind.Absolute)), null)
            : new DesktopPageBackground(null, new SolidColorBrush(ParseColour(background.Colour)));

        return pageBackground;
    }

    private static Color ParseColour(string? value)
    {
        if (value is { Length: 7 } && value[0] == '#' &&
            byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red) &&
            byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green) &&
            byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
        {
            return Color.FromArgb(255, red, green, blue);
        }

        return Color.FromArgb(255, 32, 32, 32);
    }
}
