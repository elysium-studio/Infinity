namespace Infinity.Shell;

public readonly record struct DesktopWindowDragAnchor(double HorizontalRatio, double VerticalRatio)
{
    public static bool TryCreate(double pointerX, double pointerY, DesktopSnapPlacement visibleBounds, out DesktopWindowDragAnchor anchor)
    {
        anchor = default;
        if (!double.IsFinite(pointerX) || !double.IsFinite(pointerY) || !double.IsFinite(visibleBounds.CanvasX) || !double.IsFinite(visibleBounds.CanvasY) || !double.IsFinite(visibleBounds.Width) || !double.IsFinite(visibleBounds.Height) || visibleBounds.Width <= 0 || visibleBounds.Height <= 0)
        {
            return false;
        }

        anchor = new(Math.Clamp((pointerX - visibleBounds.CanvasX) / visibleBounds.Width, 0, 1), Math.Clamp((pointerY - visibleBounds.CanvasY) / visibleBounds.Height, 0, 1));
        return true;
    }

    public (double X, double Y) GetOffset(double width, double height) => (HorizontalRatio * width, VerticalRatio * height);
}
