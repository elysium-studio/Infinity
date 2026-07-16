using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public class DwmWindowPreviewTests
{
    [Fact]
    public void PlacementIsForwardedToTheOwningSurface()
    {
        TestSurface surface = new();
        DwmWindowPreview preview = new(surface, new IntPtr(42), 7);

        preview.SetPlacement(12.0, 24.0, 300.0, 200.0, 5, true, true);

        Assert.Same(preview, surface.AppliedPreview);
        Assert.Equal((12.0, 24.0, 300.0, 200.0, 5, true, true), surface.Placement);
    }

    [Fact]
    public void DisposeRemovesPreviewOnceAndRejectsLaterUpdates()
    {
        TestSurface surface = new();
        DwmWindowPreview preview = new(surface, new IntPtr(42), 7);

        Parallel.For(0, 16, _ => preview.Dispose());
        preview.SetPlacement(12.0, 24.0, 300.0, 200.0, 5, true, true);

        Assert.Equal(1, surface.RemoveCount);
        Assert.Null(surface.AppliedPreview);
    }

    [Fact]
    public void SurfaceShutdownInvalidatesOwnedPreviews()
    {
        DwmWindowPreviewSurface surface = new(NullLogger<DwmWindowPreviewSurface>.Instance);
        IWindowPreview preview = Assert.IsType<DwmWindowPreview>(surface.CreatePreview(new IntPtr(42)));

        surface.Dispose();
        preview.SetPlacement(12.0, 24.0, 300.0, 200.0, 5, true, true);
        preview.Dispose();

        Assert.Null(surface.CreatePreview(new IntPtr(43)));
    }

    private class TestSurface : IDwmWindowPreviewSurface
    {
        private int removeCount;

        public DwmWindowPreview? AppliedPreview { get; private set; }

        public (double X, double Y, double Width, double Height, int ZIndex, bool IsVisible, bool IsElevated) Placement { get; private set; }

        public int RemoveCount => Volatile.Read(ref removeCount);

        public void Apply(DwmWindowPreview preview,
            double x,
            double y,
            double width,
            double height,
            int zIndex,
            bool isVisible,
            bool isElevated)
        {
            AppliedPreview = preview;
            Placement = (x, y, width, height, zIndex, isVisible, isElevated);
        }

        public void Remove(DwmWindowPreview preview) => Interlocked.Increment(ref removeCount);
    }
}
