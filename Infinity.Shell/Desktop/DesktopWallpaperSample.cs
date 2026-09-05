namespace Infinity.Shell;

// A small, oriented BGRA image shared by all foreground sampling locations.
public sealed record DesktopWallpaperSample(int Width, int Height, byte[] Pixels)
{
    public uint? Sample(int monitorWidth, int monitorHeight, double x, double y, double verticalAlignment)
    {
        if (Width <= 0 || Height <= 0 || Pixels.LongLength != (long)Width * Height * 4 ||
            monitorWidth <= 0 || monitorHeight <= 0 || !double.IsFinite(x) || !double.IsFinite(y) ||
            !double.IsFinite(verticalAlignment)) return null;

        double scale = Math.Max(monitorWidth / (double)Width, monitorHeight / (double)Height);
        double centerX = (x + ((Width * scale - monitorWidth) * 0.5)) / scale;
        double centerY = (y + ((Height * scale - monitorHeight) * verticalAlignment)) / scale;
        double radius = 64 / scale;
        int left = (int)Math.Clamp(Math.Floor(centerX - radius), 0, Width - 1);
        int top = (int)Math.Clamp(Math.Floor(centerY - radius), 0, Height - 1);
        int right = (int)Math.Clamp(Math.Ceiling(centerX + radius), left + 1, Width);
        int bottom = (int)Math.Clamp(Math.Ceiling(centerY + radius), top + 1, Height);

        long red = 0, green = 0, blue = 0, alpha = 0;
        for (int row = top; row < bottom; row++)
        {
            for (int column = left; column < right; column++)
            {
                int offset = (row * Width + column) * 4;
                int weight = Pixels[offset + 3];
                blue += Pixels[offset] * weight;
                green += Pixels[offset + 1] * weight;
                red += Pixels[offset + 2] * weight;
                alpha += weight;
            }
        }

        return alpha == 0 ? null : 0xff000000u | ((uint)(red / alpha) << 16) |
            ((uint)(green / alpha) << 8) | (uint)(blue / alpha);
    }
}
