using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class WindowCaptureFrameGeometryTests
{
    [Fact]
    public void UnchangedFrameCanBePresentedWithoutRecreatingThePool()
    {
        WindowCaptureFrameGeometry result = WindowCaptureFrameGeometry.Calculate(800, 600, 800, 600, 800, 600);
        Assert.True(result.CanPresent);
        Assert.False(result.RequiresPoolResize);
    }


    [Fact]
    public void GrowthInAnOldBufferRetainsThePreviousImage()
    {
        WindowCaptureFrameGeometry result = WindowCaptureFrameGeometry.Calculate(1600, 900, 800, 600, 800, 600);
        Assert.False(result.CanPresent);
        Assert.True(result.RequiresPoolResize);
    }


    [Fact]
    public void ShrinkPresentsOnlyValidContentAndRecreatesThePool()
    {
        WindowCaptureFrameGeometry result = WindowCaptureFrameGeometry.Calculate(800, 600, 1600, 900, 1600, 900);
        Assert.True(result.CanPresent);
        Assert.True(result.RequiresPoolResize);
        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
    }


    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, 0)]
    [InlineData(-1, 600)]
    public void EmptyOrInvalidContentDoesNotReplaceTheLastFrame(int width, int height)
    {
        WindowCaptureFrameGeometry result = WindowCaptureFrameGeometry.Calculate(width, height, 800, 600, 800, 600);
        Assert.False(result.CanPresent);
        Assert.False(result.RequiresPoolResize);
    }


    [Fact]
    public void ACompleteFrameAfterGrowthCanBePresented()
    {
        WindowCaptureFrameGeometry result = WindowCaptureFrameGeometry.Calculate(1600, 900, 1600, 900, 1600, 900);
        Assert.True(result.CanPresent);
        Assert.False(result.RequiresPoolResize);
    }
}
