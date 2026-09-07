namespace Infinity.Shell;

public readonly record struct DesktopWindowDragViewport(
    double ScreenX,
    double ScreenY,
    double RasterizationScale,
    double SurfaceX,
    double SurfaceY,
    double CenterX,
    double CenterY)
{
    public (double X, double Y) FromScreen(double x, double y) => ((x - ScreenX) / RasterizationScale, (y - ScreenY) / RasterizationScale);

    public (double X, double Y) ToSurface(double x, double y) => (x - SurfaceX, y - SurfaceY);

    public (double X, double Y) ToLayout(double x, double y, double scale) => ((x - SurfaceX - CenterX * (1 - scale)) / scale, (y - SurfaceY - CenterY * (1 - scale)) / scale);
}
