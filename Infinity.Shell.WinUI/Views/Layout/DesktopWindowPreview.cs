using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopWindowPreview :
    IDisposable
{
    private readonly ThumbnailCompositionPreview? preview;
    private readonly nint windowHandle;
    private double width;
    private double height;
    private bool disposed;

    public DesktopWindowPreview(nint windowHandle, Border host, ThumbnailCompositionPreview? preview)
    {
        this.windowHandle = windowHandle;
        Host = host;
        this.preview = preview;
        Host.Tapped += HandleTapped;
    }

    public event Action<nint>? Invoked;

    public Border Host { get; }

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

    public void SetZIndex(int zIndex) => Canvas.SetZIndex(Host, zIndex);

    public void SetInteractionEnabled(bool value) => Host.IsHitTestVisible = value;

    public void Update(double x,
        double y,
        double width,
        double height,
        TimeSpan? transitionDuration = null)
    {
        Host.TranslationTransition = transitionDuration.HasValue
            ? new Vector3Transition { Duration = transitionDuration.Value }
            : null;
        Host.Translation = new Vector3(ToFloat(x), ToFloat(y), 48);

        if (this.width != width || this.height != height)
        {
            this.width = width;
            this.height = height;
            Host.Width = width;
            Host.Height = height;
            preview?.Update(width, height, true);
        }
    }

    public void ClearTranslationTransition() => Host.TranslationTransition = null;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Host.Tapped -= HandleTapped;
        preview?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static float ToFloat(double value) =>
        (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);

    private void HandleTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        Invoked?.Invoke(windowHandle);
    }
}