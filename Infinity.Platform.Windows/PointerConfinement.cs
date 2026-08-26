using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public sealed class PointerConfinement :
    IPointerConfinement
{
    public unsafe bool Confine(nint windowHandle, double rasterizationScale, double left, double top, double right, double bottom)
    {
        if (windowHandle == 0 || !double.IsFinite(rasterizationScale) || rasterizationScale <= 0 || !PInvoke.GetWindowRect(new HWND(windowHandle), out RECT windowRect))
        {
            return false;
        }

        RECT bounds = new()
        {
            left = windowRect.left + ToPixel(left, rasterizationScale),
            top = windowRect.top + ToPixel(top, rasterizationScale),
            right = windowRect.left + ToPixel(right, rasterizationScale),
            bottom = windowRect.top + ToPixel(bottom, rasterizationScale)
        };

        bounds.right = Math.Max(bounds.left + 1, bounds.right);
        bounds.bottom = Math.Max(bounds.top + 1, bounds.bottom);
        return PInvoke.ClipCursor(&bounds);
    }

    public unsafe void Release() => PInvoke.ClipCursor(null);

    private static int ToPixel(double value, double rasterizationScale) => (int)Math.Round(value * rasterizationScale, MidpointRounding.AwayFromZero);
}
