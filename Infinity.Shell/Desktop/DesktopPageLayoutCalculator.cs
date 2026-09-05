namespace Infinity.Shell;

public sealed class DesktopPageLayoutCalculator
{
    public double PageSpacing => 64;

    public int CalculateVisiblePageCapacity(double viewportWidth, double desktopWidth, double overviewScale) => viewportWidth > 0 && desktopWidth > 0 && overviewScale > 0 ? (int)Math.Ceiling(viewportWidth / (desktopWidth * overviewScale)) + 4 : 0;

    public (int FirstPage, int LastPage) CalculateVisiblePageRange(int? maximumPageCount, double visualOffset, double desktopWidth, double viewportWidth, double overviewScale, double spacingProgress = 1)
    {
        if (desktopWidth <= 0 || viewportWidth <= 0 || overviewScale <= 0 || maximumPageCount == 0)
        {
            return (0, -1);
        }

        double unscaledViewportWidth = viewportWidth / overviewScale;
        double leadingSpace = Math.Max(0, (unscaledViewportWidth - desktopWidth) / 2);
        double contentOffset = CalculateContentOffset(visualOffset, desktopWidth, spacingProgress);
        double pageStride = desktopWidth + (PageSpacing * spacingProgress);
        double viewportLeft = -leadingSpace;
        double viewportRight = desktopWidth + leadingSpace;
        int firstPage = Math.Max(0, (int)Math.Ceiling((viewportLeft + contentOffset - desktopWidth) / pageStride) - 1);
        int lastPage = Math.Max(firstPage, (int)Math.Floor((viewportRight + contentOffset) / pageStride) + 1);
        if (maximumPageCount.HasValue)
        {
            lastPage = Math.Min(lastPage, maximumPageCount.Value - 1);
            firstPage = Math.Min(firstPage, lastPage);
        }

        return (firstPage, lastPage);
    }


    public double CalculatePageX(int page, double desktopWidth, double offset, double spacingProgress = 1) => (page * (desktopWidth + (PageSpacing * spacingProgress))) - CalculateContentOffset(offset, desktopWidth, spacingProgress);

    public double CalculateContentOffset(double offset, double desktopWidth, double spacingProgress = 1) => desktopWidth > 0 ? offset * ((desktopWidth + (PageSpacing * spacingProgress)) / desktopWidth) : offset;

    public double CalculateWindowX(double x, int canvasX, int windowWidth, int monitorOriginX, double desktopWidth, double offset, double spacingProgress = 1)
    {
        if (desktopWidth <= 0)
        {
            return x;
        }

        double windowCenter = canvasX - monitorOriginX + (windowWidth / 2.0);
        int page = Math.Max(0, (int)Math.Floor(windowCenter / desktopWidth));
        double spacing = PageSpacing * spacingProgress;
        double gapOffset = (page * spacing) - ((offset / desktopWidth) * spacing);
        return x + gapOffset;
    }
}
