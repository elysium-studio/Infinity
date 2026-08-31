using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWallpaperColorSampler
{
    private const double SampleRadius = 64;

    public async Task<Color?> SampleAsync(string wallpaperPath,
        int monitorWidth,
        int monitorHeight,
        Point monitorPoint)
    {
        if (string.IsNullOrWhiteSpace(wallpaperPath) ||
            monitorWidth <= 0 ||
            monitorHeight <= 0)
        {
            return null;
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(wallpaperPath);

        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

        if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0)
        {
            return null;
        }

        double scale = Math.Max(monitorWidth / (double)decoder.PixelWidth,
            monitorHeight / (double)decoder.PixelHeight);

        if (!double.IsFinite(scale) || scale <= 0)
        {
            return null;
        }

        double scaledWidth = decoder.PixelWidth * scale;
        double scaledHeight = decoder.PixelHeight * scale;
        double sourceX = (monitorPoint.X + ((scaledWidth - monitorWidth) * 0.5)) / scale;
        double sourceY = (monitorPoint.Y + ((scaledHeight - monitorHeight) * DesktopWallpaperBrushFactory.WindowsFillVerticalAlignment)) / scale;
        double sourceRadius = SampleRadius / scale;
        BitmapBounds bounds = CreateBounds(sourceX,
            sourceY,
            sourceRadius,
            decoder.PixelWidth,
            decoder.PixelHeight);
        BitmapTransform transform = new()
        {
            Bounds = bounds,
            ScaledWidth = 1,
            ScaledHeight = 1,
            InterpolationMode = BitmapInterpolationMode.Fant
        };
        PixelDataProvider provider = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);
        byte[] pixels = provider.DetachPixelData();

        return pixels.Length < 4 ? null : Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
    }

    private static BitmapBounds CreateBounds(double centerX,
        double centerY,
        double radius,
        uint imageWidth,
        uint imageHeight)
    {
        uint left = (uint)Math.Clamp(Math.Floor(centerX - radius), 0d, imageWidth - 1d);
        uint top = (uint)Math.Clamp(Math.Floor(centerY - radius), 0d, imageHeight - 1d);
        uint right = (uint)Math.Clamp(Math.Ceiling(centerX + radius), left + 1d, imageWidth);
        uint bottom = (uint)Math.Clamp(Math.Ceiling(centerY + radius), top + 1d, imageHeight);
        return new BitmapBounds
        {
            X = left,
            Y = top,
            Width = right - left,
            Height = bottom - top
        };
    }
}
