using Infinity.Platform.Windows;

namespace Infinity.Tests;

public class DwmWindowPreviewTests
{
    [Fact]
    public void TargetIsForwardedToTheOwningSurface()
    {
        TestSurface surface = new();
        DwmWindowPreview preview = new(surface, new IntPtr(42), 7, new IntPtr(84));

        preview.Update(300.0, 200.0, true);

        Assert.Same(preview, surface.AppliedPreview);
        Assert.Equal((300.0, 200.0, true), surface.Target);
        preview.Dispose();
    }

    [Fact]
    public void DisposeRemovesPreviewOnceAndRejectsLaterUpdates()
    {
        TestSurface surface = new();
        DwmWindowPreview preview = new(surface, new IntPtr(42), 7, new IntPtr(84));

        Parallel.For(0, 16, _ => preview.Dispose());
        preview.Update(300.0, 200.0, true);

        Assert.Equal(1, surface.RemoveCount);
        Assert.Null(surface.AppliedPreview);
    }

    [Fact]
    public void ExposesTheNativeCompositionVisual()
    {
        TestSurface surface = new();
        DwmWindowPreview preview = new(surface, new IntPtr(42), 7, new IntPtr(84));

        Assert.Equal(new IntPtr(84), preview.Visual);
        preview.Dispose();
    }

    private class TestSurface : IDwmWindowPreviewSurface
    {
        private int removeCount;

        public DwmWindowPreview? AppliedPreview { get; private set; }

        public (double Width, double Height, bool IsVisible) Target { get; private set; }

        public int RemoveCount => Volatile.Read(ref removeCount);

        public void Apply(DwmWindowPreview preview,
            double width,
            double height,
            bool isVisible)
        {
            AppliedPreview = preview;
            Target = (width, height, isVisible);
        }

        public void Remove(DwmWindowPreview preview) => Interlocked.Increment(ref removeCount);
    }
}
