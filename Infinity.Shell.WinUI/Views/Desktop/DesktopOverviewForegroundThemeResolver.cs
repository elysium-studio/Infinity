using System;
using System.Globalization;
using System.Threading.Tasks;
using Infinity.Platform.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewForegroundThemeResolver(DesktopWallpaperColorSampler colorSampler, ILogger<DesktopOverviewForegroundThemeResolver> logger)
{
    public async Task<ElementTheme> ResolveAsync(DesktopOverviewBackdrop backdrop, DesktopBackground background, int monitorWidth, int monitorHeight, Point monitorPoint, Brush? surfaceBrush, ElementTheme fallbackTheme)
    {
        if (backdrop == DesktopOverviewBackdrop.Dark)
        {
            return ElementTheme.Dark;
        }

        if (backdrop == DesktopOverviewBackdrop.Light)
        {
            return ElementTheme.Light;
        }

        if (string.IsNullOrWhiteSpace(background.Wallpaper))
        {
            return ResolveColour(background.Colour, fallbackTheme);
        }

        try
        {
            SurfaceColors surface = SnapshotSurface(surfaceBrush);
            Color? color = await colorSampler.SampleAsync(background.Wallpaper, monitorWidth, monitorHeight, monitorPoint);
            return color.HasValue ? ResolveColour(surface.Apply(color.Value)) : fallbackTheme;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to resolve the desktop overview foreground from wallpaper {Wallpaper}", background.Wallpaper);
            return fallbackTheme;
        }
    }


    private static ElementTheme ResolveColour(string? value, ElementTheme fallbackTheme)
    {
        if (value is not { Length: 7 } || value[0] != '#' || !byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, null, out byte red) || !byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, null, out byte green) || !byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, null, out byte blue))
        {
            return fallbackTheme;
        }

        return ResolveColour(Color.FromArgb(255, red, green, blue));
    }


    private static ElementTheme ResolveColour(Color background)
    {
        double luminance = RelativeLuminance(background);
        double whiteContrast = 1.05 / (luminance + 0.05);
        double blackContrast = (luminance + 0.05) / 0.05;
        return whiteContrast >= blackContrast ? ElementTheme.Dark : ElementTheme.Light;
    }


    private static SurfaceColors SnapshotSurface(Brush? surfaceBrush) => surfaceBrush switch
    {
        AcrylicBrush acrylic => new(acrylic.FallbackColor, acrylic.TintLuminosityOpacity ?? 1, acrylic.TintColor, acrylic.TintOpacity * acrylic.Opacity),
        SolidColorBrush solid => new(default, 0, solid.Color, (solid.Color.A / 255d) * solid.Opacity),
        _ => default
    };

    private readonly record struct SurfaceColors(Color Luminosity, double LuminosityOpacity, Color Tint, double TintOpacity)
    {
        public Color Apply(Color background) => Blend(Blend(background, Luminosity, LuminosityOpacity), Tint, TintOpacity);
    }


    private static Color Blend(Color background, Color foreground, double opacity)
    {
        double amount = Math.Clamp(opacity, 0, 1);
        return Color.FromArgb(255, BlendChannel(background.R, foreground.R, amount), BlendChannel(background.G, foreground.G, amount), BlendChannel(background.B, foreground.B, amount));
    }


    private static byte BlendChannel(byte background, byte foreground, double opacity) => (byte)Math.Round(background + ((foreground - background) * opacity));

    private static double RelativeLuminance(Color color) => (0.2126 * Linearize(color.R)) + (0.7152 * Linearize(color.G)) + (0.0722 * Linearize(color.B));

    private static double Linearize(byte channel)
    {
        double value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
