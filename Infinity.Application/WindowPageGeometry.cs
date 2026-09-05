using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class WindowPageGeometry
{
    private const double ScrollTolerance = 2.0;
    private const double MeaningfulVisibilityRatio = 0.60;

    public int GetPage(TrackedWindow window, int workspaceWidth)
    {
        double canvasX = GetSafeCanvasX(window);
        if (workspaceWidth <= 0)
        {
            return 0;
        }

        return ClampPage(Math.Floor(canvasX / workspaceWidth));
    }


    public int GetCenterPage(TrackedWindow window, int workspaceWidth)
    {
        if (workspaceWidth <= 0)
        {
            return 0;
        }

        double center = GetSafeCanvasX(window) + (GetSafeWidth(window) / 2d);
        return Math.Max(0, ClampPage(Math.Floor(center / workspaceWidth)));
    }


    public double GetTargetOffset(TrackedWindow window, int workspaceWidth, int windowPage)
    {
        double windowLeft = GetSafeCanvasX(window);
        double windowCenter = windowLeft + (GetSafeWidth(window) / 2d);
        double targetOffset = windowCenter - (workspaceWidth / 2d);
        double pageLeft = windowPage * (double)workspaceWidth;
        return IsFinite(targetOffset) ? Math.Max(pageLeft, targetOffset) : pageLeft;
    }


    public bool IsFullyVisible(TrackedWindow window, double viewportLeft, int workspaceWidth)
    {
        double viewportRight = viewportLeft + workspaceWidth;
        if (!IsValidViewport(viewportLeft, viewportRight))
        {
            return false;
        }

        double windowLeft = GetSafeCanvasX(window);
        double windowRight = windowLeft + GetSafeWidth(window);
        return windowLeft >= viewportLeft - ScrollTolerance && windowRight <= viewportRight + ScrollTolerance;
    }


    public bool IsMeaningfullyVisible(TrackedWindow window, double viewportLeft, int workspaceWidth)
    {
        double viewportRight = viewportLeft + workspaceWidth;
        if (!IsValidViewport(viewportLeft, viewportRight))
        {
            return false;
        }

        double windowLeft = GetSafeCanvasX(window);
        double windowWidth = GetSafeWidth(window);
        double windowRight = windowLeft + windowWidth;
        if (windowWidth <= 0)
        {
            return false;
        }

        if (windowLeft >= viewportLeft - ScrollTolerance && windowRight <= viewportRight + ScrollTolerance)
        {
            return true;
        }

        double windowCenter = windowLeft + (windowWidth / 2d);
        if (windowCenter >= viewportLeft && windowCenter <= viewportRight)
        {
            return true;
        }

        double visibleWidth = Math.Max(0, Math.Min(windowRight, viewportRight) - Math.Max(windowLeft, viewportLeft));
        return visibleWidth / windowWidth >= MeaningfulVisibilityRatio;
    }


    public bool AreClose(double left, double right) => IsFinite(left) && IsFinite(right) && Math.Abs(left - right) < ScrollTolerance;

    public bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private bool IsValidViewport(double left, double right) => IsFinite(left) && IsFinite(right) && right >= left;

    private static int ClampPage(double page) => page switch
    {
        > int.MaxValue => int.MaxValue,
        < int.MinValue => int.MinValue,
        _ => (int)page
    };

    private double GetSafeCanvasX(TrackedWindow window) => IsFinite(window.CanvasX) ? window.CanvasX : 0;

    private double GetSafeWidth(TrackedWindow window) => IsFinite(window.Width) && window.Width >= 0 ? window.Width : 0;
}
