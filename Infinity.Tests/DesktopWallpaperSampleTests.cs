using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWallpaperSampleTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3440, 1440)]
    [InlineData(1720, 720)]
    [InlineData(-500, -500)]
    [InlineData(5000, 5000)]
    public void UltrawideMonitorSamplesStayInsideDecodedImage(double x, double y)
    {
        DesktopWallpaperSample sample = Solid(256, 144, 20, 40, 80);
        Assert.Equal(0xff142850u, sample.Sample(3440, 1440, x, y, 1d / 3));
    }

    [Fact]
    public void AveragesTheRegionInsteadOfReturningItsFirstPixel()
    {
        DesktopWallpaperSample sample = new(2, 1, [0, 0, 255, 255, 255, 0, 0, 255]);
        Assert.Equal(0xff7f007fu, sample.Sample(2, 1, 1, 0, 1d / 3));
    }

    [Fact]
    public void FillCroppingUsesTheSameVerticalAlignmentAsTheWallpaper()
    {
        DesktopWallpaperSample sample = Solid(100, 100, 0, 0, 0);
        for (int row = 0; row < 100; row++)
            for (int column = 0; column < 100; column++)
                sample.Pixels[(row * 100 + column) * 4 + 2] = (byte)row;

        // 100x100 fills 10000x5000: top screen edge maps to source row 16.67.
        Assert.Equal(0xff100000u, sample.Sample(10000, 5000, 5000, 0, 1d / 3));
    }

    [Fact]
    public void FullyTransparentPixelsDoNotDarkenTheAverage()
    {
        DesktopWallpaperSample sample = new(2, 1, [0, 0, 255, 255, 0, 0, 0, 0]);
        Assert.Equal(0xffff0000u, sample.Sample(2, 1, 1, 0, 0));
        Assert.Null(new DesktopWallpaperSample(1, 1, [0, 0, 0, 0]).Sample(1, 1, 0, 0, 0));
    }

    [Fact]
    public void InvalidInputsReturnNoSample()
    {
        DesktopWallpaperSample sample = Solid(1, 1, 1, 2, 3);
        Assert.Null(sample.Sample(0, 1440, 0, 0, 0));
        Assert.Null(sample.Sample(3440, 1440, double.NaN, 0, 0));
        Assert.Null(sample.Sample(3440, 1440, 0, double.PositiveInfinity, 0));
        Assert.Null(new DesktopWallpaperSample(2, 2, [0, 0, 0, 255]).Sample(3440, 1440, 0, 0, 0));
    }

    [Fact]
    public void PortraitAndSinglePixelImagesCanBeSampled()
    {
        Assert.Equal(0xff142850u, Solid(144, 256, 20, 40, 80).Sample(1440, 3440, 720, 3000, 1d / 3));
        Assert.Equal(0xff142850u, Solid(1, 1, 20, 40, 80).Sample(3440, 1440, 1720, 720, 1d / 3));
    }

    private static DesktopWallpaperSample Solid(int width, int height, byte red, byte green, byte blue)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = blue;
            pixels[offset + 1] = green;
            pixels[offset + 2] = red;
            pixels[offset + 3] = 255;
        }
        return new(width, height, pixels);
    }
}
