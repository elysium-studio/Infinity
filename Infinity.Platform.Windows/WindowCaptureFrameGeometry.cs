namespace Infinity.Platform.Windows;

public readonly record struct WindowCaptureFrameGeometry(int Width, int Height, bool RequiresPoolResize, bool CanPresent)
{
    public static WindowCaptureFrameGeometry Calculate(int contentWidth, int contentHeight, int surfaceWidth, int surfaceHeight, int poolWidth, int poolHeight)
    {
        bool valid = contentWidth > 0 && contentHeight > 0;
        bool resize = valid && (contentWidth != poolWidth || contentHeight != poolHeight);
        bool complete = valid && contentWidth <= surfaceWidth && contentHeight <= surfaceHeight;
        return new(contentWidth, contentHeight, resize, complete);
    }
}
