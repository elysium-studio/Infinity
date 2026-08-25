using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Infinity.Shell.WinUI;

internal static class FluentVisualResources
{
    public static Brush GetBrush(string key, Color fallback)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object? value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    public static CornerRadius GetOverlayCornerRadius()
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("OverlayCornerRadius", out object? value))
        {
            if (value is CornerRadius cornerRadius)
            {
                return cornerRadius;
            }

            if (value is double radius)
            {
                return new CornerRadius(radius);
            }
        }

        return new CornerRadius(8);
    }
}