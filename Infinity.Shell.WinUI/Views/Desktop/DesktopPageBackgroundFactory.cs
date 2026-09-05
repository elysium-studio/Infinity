using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Media;

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
        pageBackground = !string.IsNullOrWhiteSpace(background.Wallpaper) ? new DesktopPageBackground(wallpaperSurfaceProvider.GetOrCreate(background), null) : new DesktopPageBackground(null, new SolidColorBrush(DesktopBackgroundColor.ParseOrDefault(background.Colour)));
        return pageBackground;
    }


}
