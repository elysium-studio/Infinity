using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopWindowFrameGeometry(IWindowGeometryReader reader)
{
    public DesktopSnapPlacement GetVisiblePlacement(TrackedWindow window) => ToVisible(window.Handle, new(window.CanvasX, window.CanvasY, window.Width, window.Height));

    public DesktopSnapPlacement ToVisible(nint handle, DesktopSnapPlacement outer)
    {
        (double Left, double Top, double Right, double Bottom) inset = ReadInsets(handle);
        return new(outer.CanvasX + inset.Left, outer.CanvasY + inset.Top, Math.Max(1, outer.Width - inset.Left - inset.Right), Math.Max(1, outer.Height - inset.Top - inset.Bottom));
    }


    public DesktopSnapPlacement ToOuter(nint handle, DesktopSnapPlacement visible)
    {
        (double Left, double Top, double Right, double Bottom) inset = ReadInsets(handle);
        return new(visible.CanvasX - inset.Left, visible.CanvasY - inset.Top, visible.Width + inset.Left + inset.Right, visible.Height + inset.Top + inset.Bottom);
    }


    private (double Left, double Top, double Right, double Bottom) ReadInsets(nint handle)
    {
        if (reader.IsMinimised(handle) || !reader.TryReadGeometry(handle, out int x, out int y, out int width, out int height) || !reader.TryReadVisibleGeometry(handle, out int visibleX, out int visibleY, out int visibleWidth, out int visibleHeight))
        {
            return default;
        }

        double left = (double)visibleX - x;
        double top = (double)visibleY - y;
        double right = (double)x + width - visibleX - visibleWidth;
        double bottom = (double)y + height - visibleY - visibleHeight;
        if (left < 0 || top < 0 || right < 0 || bottom < 0 || left + right >= width || top + bottom >= height || visibleWidth <= 0 || visibleHeight <= 0)
        {
            return default;
        }

        return (left, top, right, bottom);
    }
}
