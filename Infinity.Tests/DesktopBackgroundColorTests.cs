using Infinity.Shell.WinUI;
using Windows.UI;

namespace Infinity.Tests;

public sealed class DesktopBackgroundColorTests
{
    [Theory]
    [InlineData("#123456", 0x12, 0x34, 0x56)]
    [InlineData("#AaBbCc", 0xAA, 0xBB, 0xCC)]
    [InlineData("#000000", 0, 0, 0)]
    [InlineData("#FFFFFF", 255, 255, 255)]
    public void ParsesOpaqueRgbColours(string text, byte red, byte green, byte blue)
    {
        Assert.True(DesktopBackgroundColor.TryParse(text, out Color color));
        Assert.Equal(Color.FromArgb(255, red, green, blue), color);
        Assert.Equal(color, DesktopBackgroundColor.ParseOrDefault(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("#123")]
    [InlineData("#12345678")]
    [InlineData("#GGGGGG")]
    public void InvalidColoursPreserveTheExistingFallback(string? text)
    {
        Assert.False(DesktopBackgroundColor.TryParse(text, out _));
        Assert.Equal(Color.FromArgb(255, 32, 32, 32), DesktopBackgroundColor.ParseOrDefault(text));
    }
}
