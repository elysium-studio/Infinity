using System;
using System.Globalization;
using Windows.UI;

namespace Infinity.Shell.WinUI;

internal static class DesktopBackgroundColor
{
    public static Color ParseOrDefault(string? value) => TryParse(value, out Color color) ? color : Color.FromArgb(255, 32, 32, 32);

    public static bool TryParse(string? value, out Color color)
    {
        color = default;
        if (value is not { Length: 7 } || value[0] != '#' || !byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red) || !byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green) || !byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
        {
            return false;
        }

        color = Color.FromArgb(255, red, green, blue);
        return true;
    }
}
