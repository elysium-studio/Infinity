namespace Infinity.Shell;

public sealed class DesktopWallpaperPlacementCalculator
{
    public DesktopWallpaperPlacement Calculate(int monitorX, int monitorY, int monitorWidth, int monitorHeight, int workAreaX, int workAreaY)
    {
        if (monitorWidth <= 0 || monitorHeight <= 0)
        {
            return default;
        }

        double workAreaOffsetX = Math.Clamp(workAreaX - (double)monitorX, 0, monitorWidth);
        double workAreaOffsetY = Math.Clamp(workAreaY - (double)monitorY, 0, monitorHeight);
        return new(monitorWidth, monitorHeight, -workAreaOffsetX, -workAreaOffsetY);
    }
}
