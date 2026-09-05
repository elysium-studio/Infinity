namespace Infinity.Shell;

public readonly record struct DesktopWallpaperPlacement(double Width, double Height, double X, double Y)
{
    public bool IsValid => Width > 0 && Height > 0;
}
