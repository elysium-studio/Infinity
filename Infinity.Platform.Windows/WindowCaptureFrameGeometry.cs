namespace Infinity.Platform.Windows;

// Pure pixel geometry shared by the capture renderer and regression tests.
public readonly record struct WindowCaptureFrameGeometry(int Width, int Height, bool RequiresPoolResize, bool CanPresent)
{
    public static WindowCaptureFrameGeometry Calculate(int contentWidth, int contentHeight, int surfaceWidth, int surfaceHeight, int poolWidth, int poolHeight)
    {
        bool valid = contentWidth > 0 && contentHeight > 0;
        bool resize = valid && (contentWidth != poolWidth || contentHeight != poolHeight);
        // A growth frame can still have the old, smaller buffer. Never stretch
        // that partial image to impersonate a complete resized window.
        bool complete = valid && contentWidth <= surfaceWidth && contentHeight <= surfaceHeight;
        return new(contentWidth, contentHeight, resize, complete);
    }
}
