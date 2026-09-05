using System.Runtime.InteropServices;

namespace Infinity.Platform.Windows;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct DesktopWallpaperRect(int left, int top, int right, int bottom)
{
    public readonly int Left = left;
    public readonly int Top = top;
    public readonly int Right = right;
    public readonly int Bottom = bottom;
}
