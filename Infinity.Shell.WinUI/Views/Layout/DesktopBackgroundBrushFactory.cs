using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Globalization;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class DesktopBackgroundBrushFactory
{
    private DesktopBackground? background;
    private Brush? brush;

    public Brush Create(DesktopBackground background)
    {
        if (this.background == background && brush is not null)
        {
            return brush;
        }

        this.background = background;
        if (!string.IsNullOrWhiteSpace(background.Wallpaper))
        {
            brush = new ImageBrush
            {
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
                ImageSource = new BitmapImage(new Uri(background.Wallpaper, UriKind.Absolute)),
                Stretch = Stretch.UniformToFill
            };
        }
        else
        {
            brush = new SolidColorBrush(ParseColour(background.Colour));
        }

        return brush;
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
