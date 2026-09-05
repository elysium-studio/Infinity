using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopDragBoundaryCalculator(IPager pager, IScroller scroller, IWorkspace workspace, DesktopPageLayoutCalculator pageLayoutCalculator)
{
    private double workAreaOffsetY;

    public void SetWorkAreaOffsetY(double value) => workAreaOffsetY = double.IsFinite(value) ? value : 0;

    public (double X, double Y) Constrain(double pointerX, double pointerY, double viewportWidth, double viewportHeight, double overviewScale)
    {
        DesktopDragBounds bounds = GetBounds(viewportWidth, viewportHeight, overviewScale);
        if (!double.IsFinite(pointerX) || !double.IsFinite(pointerY) || !bounds.IsValid)
        {
            return (pointerX, pointerY);
        }

        return (Math.Clamp(pointerX, bounds.MinimumX, bounds.MaximumX), Math.Clamp(pointerY, bounds.MinimumY, bounds.MaximumY));
    }


    public double ConstrainHorizontal(double pointerX, double viewportWidth, double overviewScale)
    {
        if (!double.IsFinite(pointerX) || !double.IsFinite(viewportWidth) || viewportWidth <= 0 || !HasValidDesktop(overviewScale))
        {
            return pointerX;
        }

        DesktopDragBounds bounds = GetBounds(viewportWidth, workspace.Height, overviewScale);
        return bounds.IsValid ? Math.Clamp(pointerX, bounds.MinimumX, bounds.MaximumX) : pointerX;
    }


    public DesktopDragBounds GetBounds(double viewportWidth, double viewportHeight, double overviewScale)
    {
        if (!double.IsFinite(viewportWidth) || !double.IsFinite(viewportHeight) || viewportWidth <= 0 || viewportHeight <= 0 || !HasValidDesktop(overviewScale))
        {
            return default;
        }

        (double minimumX, double maximumX) = GetHorizontalBounds(viewportWidth, overviewScale);
        double pageHeight = workspace.Height * overviewScale;
        double minimumY = Math.Max(0, workAreaOffsetY + ((workspace.Height / 2.0) * (1 - overviewScale)));
        double maximumY = Math.Min(viewportHeight, minimumY + pageHeight);
        return new(minimumX, minimumY, maximumX, maximumY);
    }


    public DesktopDragBounds GetCenteredPageBounds(double viewportWidth, double viewportHeight, double overviewScale)
    {
        if (!double.IsFinite(viewportWidth) || !double.IsFinite(viewportHeight) || viewportWidth <= 0 || viewportHeight <= 0 || !HasValidDesktop(overviewScale))
        {
            return default;
        }

        (double minimumX, double maximumX) = GetCenteredPageHorizontalBounds(viewportWidth, overviewScale);
        double pageHeight = Math.Min(viewportHeight, workspace.Height * overviewScale);
        double minimumY = Math.Max(0, workAreaOffsetY + ((workspace.Height / 2.0) * (1 - overviewScale)));
        double maximumY = Math.Min(viewportHeight, minimumY + pageHeight);
        return new(minimumX, minimumY, maximumX, maximumY);
    }


    public (double MinimumX, double MaximumX) GetCenteredPageHorizontalBounds(double viewportWidth, double overviewScale)
    {
        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0 || !HasValidDesktop(overviewScale))
        {
            return default;
        }

        double pageWidth = Math.Min(viewportWidth, workspace.Width * overviewScale);
        double minimumX = Math.Max(0, (viewportWidth - pageWidth) / 2);
        return (minimumX, Math.Min(viewportWidth, minimumX + pageWidth));
    }


    public (double X, double Y) ConstrainToCenteredPage(double pointerX, double pointerY, double viewportWidth, double viewportHeight, double overviewScale)
    {
        DesktopDragBounds bounds = GetCenteredPageBounds(viewportWidth, viewportHeight, overviewScale);
        if (!double.IsFinite(pointerX) || !double.IsFinite(pointerY) || !bounds.IsValid)
        {
            return (pointerX, pointerY);
        }

        return (Math.Clamp(pointerX, bounds.MinimumX, bounds.MaximumX), Math.Clamp(pointerY, bounds.MinimumY, bounds.MaximumY));
    }


    private (double MinimumX, double MaximumX) GetHorizontalBounds(double viewportWidth, double overviewScale)
    {
        double visualOffset = scroller.VisualOffset;
        double contentOffset = pageLayoutCalculator.CalculateContentOffset(visualOffset, workspace.Width);
        double firstPageX = -contentOffset;
        double minimumX = ToScreenX(firstPageX, viewportWidth, overviewScale);
        double maximumX = viewportWidth;
        if (pager.MaxPages is int maximumPageCount && maximumPageCount > 0)
        {
            double lastPageX = ((maximumPageCount - 1) * (workspace.Width + pageLayoutCalculator.PageSpacing)) - contentOffset;
            maximumX = ToScreenX(lastPageX + workspace.Width, viewportWidth, overviewScale);
        }

        minimumX = Math.Clamp(minimumX, 0, viewportWidth);
        maximumX = Math.Clamp(maximumX, minimumX, viewportWidth);
        return (minimumX, maximumX);
    }


    private bool HasValidDesktop(double overviewScale) => double.IsFinite(overviewScale) && overviewScale > 0 && workspace.Width > 0 && workspace.Height > 0;

    private static double ToScreenX(double desktopX, double viewportWidth, double overviewScale) => (viewportWidth / 2) + ((desktopX - (viewportWidth / 2)) * overviewScale);
}
