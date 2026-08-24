using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopWindowPreview :
    IDisposable
{
    private readonly ThumbnailCompositionPreview? preview;
    private double width;
    private double height;
    private bool disposed;

    public DesktopWindowPreview(Grid host, ThumbnailCompositionPreview? preview)
    {
        Host = host;
        this.preview = preview;
        ElementCompositionPreview.SetIsTranslationEnabled(host, true);
    }

    public Grid Host { get; }

    public double SourceWidth { get; private set; }

    public double SourceHeight { get; private set; }

    public void RefreshSourceSize(TrackedWindow trackedWindow, IWindowGeometryReader geometryReader)
    {
        if (geometryReader.TryReadVisibleGeometry(trackedWindow.Handle,
            out _,
            out _,
            out int visibleWidth,
            out int visibleHeight))
        {
            SourceWidth = visibleWidth;
            SourceHeight = visibleHeight;
            return;
        }

        SourceWidth = trackedWindow.Width;
        SourceHeight = trackedWindow.Height;
    }

    public void Update(double x, double y, double width, double height, int? zIndex)
    {
        if (zIndex.HasValue)
        {
            Canvas.SetZIndex(Host, zIndex.Value);
        }

        Host.Translation = new Vector3(ToFloat(x), ToFloat(y), 0);

        if (this.width != width || this.height != height)
        {
            this.width = width;
            this.height = height;
            Host.Width = width;
            Host.Height = height;
            preview?.Update(width, height, true);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        preview?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static float ToFloat(double value) =>
        (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);
}
