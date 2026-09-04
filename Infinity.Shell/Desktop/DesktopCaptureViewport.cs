namespace Infinity.Shell;

/// <summary>Capture bounds in the unscaled overview canvas coordinate system.</summary>
public readonly record struct DesktopCaptureViewport(
    double Left, double Top, double Right, double Bottom, double Prewarm, double Retention)
{
    public static DesktopCaptureViewport Create(double screenWidth, double screenHeight,
        double workWidth, double workHeight, double offsetX, double offsetY, double scale)
    {
        if (!double.IsFinite(screenWidth) || !double.IsFinite(screenHeight) ||
            !double.IsFinite(workWidth) || !double.IsFinite(workHeight) ||
            !double.IsFinite(offsetX) || !double.IsFinite(offsetY) ||
            !double.IsFinite(scale) || screenWidth <= 0 || screenHeight <= 0 ||
            workWidth <= 0 || workHeight <= 0 || scale <= 0 || scale > 1)
        {
            return default;
        }

        double centerX = workWidth / 2;
        double centerY = workHeight / 2;
        // Include both ends of the desktop/overview zoom, without reading animated
        // Composition properties or changing the existing animation pipeline.
        return new(
            Math.Min(-offsetX, centerX + (-offsetX - centerX) / scale),
            Math.Min(-offsetY, centerY + (-offsetY - centerY) / scale),
            Math.Max(screenWidth - offsetX, centerX + (screenWidth - offsetX - centerX) / scale),
            Math.Max(screenHeight - offsetY, centerY + (screenHeight - offsetY - centerY) / scale),
            workWidth * 0.5,
            workWidth * 0.25);
    }

    public bool ShouldCapture(double x, double y, double width, double height, bool alreadyCapturing)
    {
        if (Right <= Left || Bottom <= Top || !double.IsFinite(x) || !double.IsFinite(y) ||
            !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return false;
        }

        double margin = Prewarm + (alreadyCapturing ? Retention : 0);
        return x < Right + margin && x + width > Left - margin &&
            y < Bottom && y + height > Top;
    }
}
