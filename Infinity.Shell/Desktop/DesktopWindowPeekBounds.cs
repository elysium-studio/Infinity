namespace Infinity.Shell;

public readonly record struct DesktopWindowPeekBounds(
    double X,
    double Y,
    double Width,
    double Height)
{
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;

    public static DesktopWindowPeekBounds Center(double imageWidth, double imageHeight, double viewportWidth, double viewportHeight)
    {
        if (!double.IsFinite(imageWidth) || !double.IsFinite(imageHeight) || !double.IsFinite(viewportWidth) || !double.IsFinite(viewportHeight) || imageWidth <= 0 || imageHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return default;
        }

        double scale = Math.Min(1, Math.Min(Math.Max(1, viewportWidth - 80) / imageWidth, Math.Max(1, viewportHeight - 80) / imageHeight));
        double width = imageWidth * scale;
        double height = imageHeight * scale;
        return new((viewportWidth - width) / 2, (viewportHeight - height) / 2, width, height);
    }

    public DesktopWindowPeekBounds Fit(DesktopWindowPeekBounds image)
    {
        if (!IsValid || !image.IsValid)
        {
            return image;
        }

        double scale = Math.Min(Width / image.Width, Height / image.Height);
        double width = image.Width * scale;
        double height = image.Height * scale;
        return new(X + (Width - width) / 2, Y + (Height - height) / 2, width, height);
    }
}
