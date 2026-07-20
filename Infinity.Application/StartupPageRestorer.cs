using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public class StartupPageRestorer(IWindowStore store,
    IPanState state,
    IWorkspace workspace,
    IWindowGeometryReader geometryReader,
    ILogger<StartupPageRestorer> logger)
{
    public void Restore()
    {
        int workspaceWidth = workspace.Width;

        if (workspaceWidth <= 0)
        {
            return;
        }

        StartupWindowBounds? leftmostOffscreenWindow = store
            .Select(GetVisibleBounds)
            .Where(bounds => bounds.Top < workspace.Height && bounds.Bottom > 0)
            .Where(bounds => bounds.Right <= 0)
            .MinBy(bounds => bounds.Left);

        if (leftmostOffscreenWindow is null)
        {
            return;
        }

        long distanceFromCurrentPage = -(long)leftmostOffscreenWindow.Left;
        long pages = (distanceFromCurrentPage + workspaceWidth - 1) / workspaceWidth;
        long pageShift = pages * workspaceWidth;

        if (pageShift is <= 0 or > int.MaxValue || !CanShiftWindows(pageShift))
        {
            logger.LogWarning("Startup page offset exceeded the supported canvas range");
            return;
        }

        logger.LogInformation(
            "Offscreen window detected during startup. Handle={WindowHandle}, Left={WindowLeft}, Top={WindowTop}, Right={WindowRight}, Bottom={WindowBottom}, PageShift={PageShift}",
            leftmostOffscreenWindow.Handle,
            leftmostOffscreenWindow.Left,
            leftmostOffscreenWindow.Top,
            leftmostOffscreenWindow.Right,
            leftmostOffscreenWindow.Bottom,
            pageShift);

        foreach (TrackedWindow trackedWindow in store)
        {
            trackedWindow.CanvasX += (int)pageShift;
        }

        state.SetOffset(pageShift);
    }

    private StartupWindowBounds GetVisibleBounds(TrackedWindow window)
    {
        if (geometryReader.TryReadVisibleGeometry(window.Handle,
            out int x,
            out int y,
            out int width,
            out int height))
        {
            return new(window.Handle, x, y, (long)x + width, (long)y + height);
        }

        return new(window.Handle,
            window.CanvasX,
            window.CanvasY,
            (long)window.CanvasX + window.Width,
            (long)window.CanvasY + window.Height);
    }

    private bool CanShiftWindows(long pageShift) =>
        store.All(window => (long)window.CanvasX + pageShift is >= int.MinValue and <= int.MaxValue);

    private record StartupWindowBounds(IntPtr Handle, int Left, int Top, long Right, long Bottom);
}
