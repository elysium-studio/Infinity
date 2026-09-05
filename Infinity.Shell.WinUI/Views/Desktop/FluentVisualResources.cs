using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using XamlApplication = Microsoft.UI.Xaml.Application;

namespace Infinity.Shell.WinUI;

internal static class FluentVisualResources
{
    public static Brush GetBrush(string key, Color fallback)
    {
        if (XamlApplication.Current.Resources.TryGetValue(key, out object? value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }


    public static CornerRadius GetOverlayCornerRadius()
    {
        if (XamlApplication.Current.Resources.TryGetValue("OverlayCornerRadius", out object? value))
        {
            if (value is CornerRadius cornerRadius)
            {
                return cornerRadius;
            }

            if (value is double radius)
            {
                return new(radius);
            }
        }

        return new(8);
    }
}
