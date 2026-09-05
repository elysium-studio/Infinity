using Infinity.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWallpaperColorSampler
{
    private const int MaximumSampleDimension = 256;
    private readonly object cacheGate = new();
    private readonly Dictionary<(string Path, long Length, long Modified), Task<DesktopWallpaperSample?>> samples = [];

    public async Task<Color?> SampleAsync(string wallpaperPath, int monitorWidth, int monitorHeight, Point monitorPoint)
    {
        if (string.IsNullOrWhiteSpace(wallpaperPath) || monitorWidth <= 0 || monitorHeight <= 0 ||
            !double.IsFinite(monitorPoint.X) || !double.IsFinite(monitorPoint.Y)) return null;

        FileInfo file = new(wallpaperPath);
        if (!file.Exists) return null;
        var key = (file.FullName.ToUpperInvariant(), file.Length, file.LastWriteTimeUtc.Ticks);
        Task<DesktopWallpaperSample?> pending;
        lock (cacheGate)
        {
            if (!samples.TryGetValue(key, out pending!))
            {
                // Bound retained memory; simultaneous requests share the same decode.
                if (samples.Count >= 4) samples.Clear();
                pending = DecodeAsync(file.FullName);
                samples.Add(key, pending);
            }
        }

        DesktopWallpaperSample? sample;
        try { sample = await pending; }
        catch
        {
            // A transient read failure must not poison the cache permanently.
            lock (cacheGate)
            {
                if (samples.TryGetValue(key, out var current) && ReferenceEquals(current, pending)) samples.Remove(key);
            }
            throw;
        }

        uint? color = sample?.Sample(monitorWidth, monitorHeight, monitorPoint.X, monitorPoint.Y,
            DesktopWallpaperBrushFactory.WindowsFillVerticalAlignment);
        return color is uint argb ? Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16),
            (byte)(argb >> 8), (byte)argb) : null;
    }

    private static async Task<DesktopWallpaperSample?> DecodeAsync(string path)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(path);
        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0) return null;

        double scale = Math.Min(1, MaximumSampleDimension / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        uint width = Math.Max(1u, (uint)Math.Round(decoder.PixelWidth * scale));
        uint height = Math.Max(1u, (uint)Math.Round(decoder.PixelHeight * scale));
        // BitmapTransform scales BEFORE cropping. Decode the complete small image;
        // never apply native-resolution crop bounds to a scaled decoder output.
        BitmapTransform transform = new()
        {
            ScaledWidth = width,
            ScaledHeight = height,
            InterpolationMode = BitmapInterpolationMode.Fant
        };
        PixelDataProvider provider = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight, transform, ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);
        bool rotated = decoder.OrientedPixelWidth != decoder.PixelWidth;
        return new DesktopWallpaperSample((int)(rotated ? height : width), (int)(rotated ? width : height),
            provider.DetachPixelData());
    }
}
