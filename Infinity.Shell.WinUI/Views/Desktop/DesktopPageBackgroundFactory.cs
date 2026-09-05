using System;
using System.Globalization;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class DesktopPageBackgroundFactory(DesktopWallpaperSurfaceProvider wallpaperSurfaceProvider)
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
        pageBackground = !string.IsNullOrWhiteSpace(background.Wallpaper) ? new DesktopPageBackground(wallpaperSurfaceProvider.GetOrCreate(background), null) : new DesktopPageBackground(null, new SolidColorBrush(ParseColour(background.Colour)));
        return pageBackground;
    }


    private static Color ParseColour(string? value)
    {
        if (value is { Length: 7 } && value[0] == '#' && byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red) && byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green) && byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
        {
            return Color.FromArgb(255, red, green, blue);
        }

        return Color.FromArgb(255, 32, 32, 32);
    }
}
