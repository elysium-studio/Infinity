using System.Collections.Generic;
using Windows.Graphics;
using Windows.Win32.Graphics.Gdi;

namespace Infinity.UI.WinUI;

internal sealed class DesktopOverlayMonitorSpan(RectInt32 bounds, IReadOnlySet<nint> monitorHandles)
{
    public RectInt32 Bounds { get; } = bounds;

    public bool IsSpanning => monitorHandles.Count > 1;

    public bool Contains(HMONITOR monitor) => monitorHandles.Contains((nint)monitor);
}
