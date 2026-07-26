using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class DwmWindowPreviewTests
{
    [Fact]
    public void TargetIsForwardedToTheOwningSurface()
    {
        TestSurface surface = new();
        DwmWindowPreview preview = new(surface, new IntPtr(42), 7);

        preview.SetTarget(new IntPtr(84), 300.0, 200.0, true);

        Assert.Same(preview, surface.AppliedPreview);
        Assert.Equal((new IntPtr(84), 300.0, 200.0, true), surface.Target);
    }

    [Fact]
    public void DisposeRemovesPreviewOnceAndRejectsLaterUpdates()
    {
        TestSurface surface = new();
        DwmWindowPreview preview = new(surface, new IntPtr(42), 7);

        Parallel.For(0, 16, _ => preview.Dispose());
        preview.SetTarget(new IntPtr(84), 300.0, 200.0, true);

        Assert.Equal(1, surface.RemoveCount);
        Assert.Null(surface.AppliedPreview);
    }

    [Fact]
    public void SurfaceShutdownInvalidatesOwnedPreviews()
    {
        DwmWindowPreviewSurface surface = new(NullLogger<DwmWindowPreviewSurface>.Instance);
        IWindowPreview preview = Assert.IsType<DwmWindowPreview>(surface.CreatePreview(new IntPtr(42)));

        surface.Dispose();
        preview.SetTarget(new IntPtr(84), 300.0, 200.0, true);
        preview.Dispose();

        Assert.Null(surface.CreatePreview(new IntPtr(43)));
    }

    private sealed class TestSurface :
        IDwmWindowPreviewSurface
    {
        private int removeCount;

        public DwmWindowPreview? AppliedPreview { get; private set; }

        public (nint SharedTargetHandle, double Width, double Height, bool IsVisible) Target { get; private set; }

        public int RemoveCount => Volatile.Read(ref removeCount);

        public void Apply(DwmWindowPreview preview,
            nint sharedTargetHandle,
            double width,
            double height,
            bool isVisible)
        {
            AppliedPreview = preview;
            Target = (sharedTargetHandle, width, height, isVisible);
        }

        public void Remove(DwmWindowPreview preview) => Interlocked.Increment(ref removeCount);
    }
}
