using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;
using Windows.Foundation;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopWindowPreview :
    IDisposable
{
    private const float ShadowDepth = 72;
    private const double DragThreshold = 4;

    private readonly ThumbnailCompositionPreview? preview;
    private readonly ITrackedWindowDragController dragController;
    private readonly nint windowHandle;
    private readonly double layoutScale;
    private uint? dragPointerId;
    private Point dragStartPoint;
    private UIElement? dragCoordinateRoot;
    private double dragHorizontalDelta;
    private double dragVerticalDelta;
    private double x;
    private double y;
    private double width;
    private double height;
    private bool interactionEnabled;
    private bool isFilterMatch = true;
    private bool isDragging;
    private bool suppressNextTap;
    private bool disposed;

    public DesktopWindowPreview(nint windowHandle,
        Border host,
        ThumbnailCompositionPreview? preview,
        ITrackedWindowDragController dragController,
        double layoutScale)
    {
        this.windowHandle = windowHandle;
        Host = host;
        this.preview = preview;
        this.dragController = dragController;
        this.layoutScale = layoutScale;
        Host.PointerPressed += HandlePointerPressed;
        Host.PointerMoved += HandlePointerMoved;
        Host.PointerReleased += HandlePointerReleased;
        Host.PointerCanceled += HandlePointerCanceled;
        Host.PointerCaptureLost += HandlePointerCaptureLost;
        Host.Tapped += HandleTapped;
    }

    public event Action<nint>? Invoked;

    public event Action<nint>? PositionChanged;

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

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;

        if (!value)
        {
            CompleteDrag();
            Host.ReleasePointerCaptures();
        }

        ApplyInteractionState();
    }

    public void SetFilterMatch(bool value)
    {
        isFilterMatch = value;

        if (!value)
        {
            CompleteDrag();
            Host.ReleasePointerCaptures();
        }

        Host.Opacity = value ? 1 : 0;
        ApplyInteractionState();
    }

    public void Update(double x,
        double y,
        double width,
        double height,
        TimeSpan? transitionDuration = null)
    {
        Host.TranslationTransition = transitionDuration.HasValue
            ? new Vector3Transition { Duration = transitionDuration.Value }
            : null;
        this.x = x;
        this.y = y;
        ApplyTranslation();

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
        CompleteDrag();
        Host.ReleasePointerCaptures();
        Host.PointerPressed -= HandlePointerPressed;
        Host.PointerMoved -= HandlePointerMoved;
        Host.PointerReleased -= HandlePointerReleased;
        Host.PointerCanceled -= HandlePointerCanceled;
        Host.PointerCaptureLost -= HandlePointerCaptureLost;
        Host.Tapped -= HandleTapped;
        preview?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static float ToFloat(double value) =>
        (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);

    private void HandleTapped(object sender, TappedRoutedEventArgs args)
    {
        if (suppressNextTap)
        {
            suppressNextTap = false;
            args.Handled = true;
            return;
        }

        args.Handled = true;
        Invoked?.Invoke(windowHandle);
    }

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        suppressNextTap = false;
        var point = args.GetCurrentPoint(Host);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        UIElement coordinateRoot = Host.XamlRoot?.Content as UIElement ?? Host;

        if (!Host.CapturePointer(args.Pointer))
        {
            return;
        }

        dragPointerId = args.Pointer.PointerId;
        dragCoordinateRoot = coordinateRoot;
        dragStartPoint = args.GetCurrentPoint(coordinateRoot).Position;
    }

    private void HandlePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId || dragCoordinateRoot is null)
        {
            return;
        }

        Point currentPoint = args.GetCurrentPoint(dragCoordinateRoot).Position;
        double horizontalDelta = currentPoint.X - dragStartPoint.X;
        double verticalDelta = currentPoint.Y - dragStartPoint.Y;

        if (!isDragging)
        {
            double distance = Math.Sqrt(horizontalDelta * horizontalDelta + verticalDelta * verticalDelta);

            if (distance < DragThreshold)
            {
                return;
            }

            if (!double.IsFinite(layoutScale) || layoutScale <= 0 || !dragController.Begin(windowHandle))
            {
                CompleteDrag();
                Host.ReleasePointerCapture(args.Pointer);
                return;
            }

            isDragging = true;
            suppressNextTap = true;
            ClearTranslationTransition();
        }

        dragHorizontalDelta = horizontalDelta / layoutScale;
        dragVerticalDelta = verticalDelta / layoutScale;
        ApplyTranslation();
        args.Handled = true;
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        bool wasDragging = isDragging;
        CompleteDrag();
        Host.ReleasePointerCapture(args.Pointer);
        args.Handled = wasDragging;
    }

    private void HandlePointerCanceled(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        CompleteDrag();
        Host.ReleasePointerCapture(args.Pointer);
        args.Handled = true;
    }

    private void HandlePointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId == args.Pointer.PointerId)
        {
            CompleteDrag();
        }
    }

    private void CompleteDrag()
    {
        bool wasDragging = isDragging;
        double horizontalDelta = dragHorizontalDelta;
        double verticalDelta = dragVerticalDelta;
        dragPointerId = null;
        dragCoordinateRoot = null;
        dragHorizontalDelta = 0;
        dragVerticalDelta = 0;
        isDragging = false;

        if (wasDragging)
        {
            bool moved = dragController.Move(windowHandle, horizontalDelta, verticalDelta);
            dragController.End(windowHandle);

            if (moved)
            {
                PositionChanged?.Invoke(windowHandle);
                return;
            }
        }

        ApplyTranslation();
    }

    private void ApplyTranslation() => Host.Translation = new Vector3(ToFloat(x + dragHorizontalDelta),
        ToFloat(y + dragVerticalDelta),
        ShadowDepth);

    private void ApplyInteractionState() => Host.IsHitTestVisible = interactionEnabled && isFilterMatch;
}
