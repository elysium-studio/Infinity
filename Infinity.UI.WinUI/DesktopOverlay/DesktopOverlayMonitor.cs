using Windows.Graphics;
using Windows.Win32.Graphics.Gdi;

namespace Infinity.UI.WinUI;

internal readonly record struct DesktopOverlayMonitor(HMONITOR Handle, RectInt32 Bounds, uint DpiX, uint DpiY);
