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
    private const int DraggedZIndex = 1_000_000;

    private readonly ThumbnailCompositionPreview? preview;
    private readonly Border focusHost;
    private readonly Grid focusVisual;
    private readonly ITrackedWindowDragController dragController;
    private readonly DesktopWindowDragDeltaResolver dragDeltaResolver;
    private readonly IWindowPreviewSurface previewSurface;
    private readonly Canvas coordinateHost;
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
    private int zIndex;

    public DesktopWindowPreview(nint windowHandle,
        Border host,
        Border focusHost,
        ThumbnailCompositionPreview? preview,
        Grid focusVisual,
        ITrackedWindowDragController dragController,
        DesktopWindowDragDeltaResolver dragDeltaResolver,
        IWindowPreviewSurface previewSurface,
        Canvas coordinateHost,
        double layoutScale)
    {
        this.windowHandle = windowHandle;
        Host = host;
        this.focusHost = focusHost;
        this.preview = preview;
        this.focusVisual = focusVisual;
        this.dragController = dragController;
        this.dragDeltaResolver = dragDeltaResolver;
        this.previewSurface = previewSurface;
        this.coordinateHost = coordinateHost;
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

    public event Action<nint>? Promoted;

    public event Action<nint>? PromotionReleased;

    public Border Host { get; }

    public Border FocusHost => focusHost;

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

    public void SetZIndex(int value)
    {
        zIndex = value;

        if (!previewSurface.IsElevated(windowHandle))
        {
            Canvas.SetZIndex(Host, value);
        }

        Canvas.SetZIndex(focusHost, value);
    }

    public void SetPromoted(bool value)
    {
        int valueToApply = value ? DraggedZIndex : zIndex;
        Canvas.SetZIndex(Host, valueToApply);
        Canvas.SetZIndex(focusHost, valueToApply);
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;

        if (!value)
        {
            CompleteDrag();
            Host.ReleasePointerCaptures();
            ReleasePromotion();
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
            ReleasePromotion();
        }

        double opacity = value ? 1 : 0;
        Host.Opacity = opacity;
        focusHost.Opacity = opacity;
        ApplyInteractionState();
    }

    public void SetSelected(bool value) =>
        focusVisual.Visibility = value ? Visibility.Visible : Visibility.Collapsed;

    public void Update(double x,
        double y,
        double width,
        double height,
        TimeSpan? transitionDuration = null)
    {
        Host.TranslationTransition = transitionDuration.HasValue
            ? new Vector3Transition { Duration = transitionDuration.Value }
            : null;
        focusHost.TranslationTransition = transitionDuration.HasValue
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
            focusHost.Width = width;
            focusHost.Height = height;
            preview?.Update(width, height, true);
        }

        UpdateElevatedPreview();
    }

    public void ClearTranslationTransition()
    {
        Host.TranslationTransition = null;
        focusHost.TranslationTransition = null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CompleteDrag();
        ReleasePromotion();
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

        if (ShowElevatedPreview())
        {
            Promoted?.Invoke(windowHandle);
        }

        args.Handled = true;
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
        UpdateElevatedPreview();
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
            double resolvedHorizontalDelta = dragDeltaResolver.ResolveHorizontalDelta(windowHandle, horizontalDelta);
            bool moved = dragController.Move(windowHandle, resolvedHorizontalDelta, verticalDelta);
            dragController.End(windowHandle);

            if (moved)
            {
                PositionChanged?.Invoke(windowHandle);
                return;
            }
        }

        ApplyTranslation();
    }

    private void ApplyTranslation()
    {
        Vector3 translation = new(ToFloat(x + dragHorizontalDelta),
            ToFloat(y + dragVerticalDelta),
            ShadowDepth);
        Host.Translation = translation;
        focusHost.Translation = translation;
    }

    private bool ShowElevatedPreview()
    {
        if (!TryGetElevatedBounds(out int left, out int top, out int pixelWidth, out int pixelHeight))
        {
            return false;
        }

        return previewSurface.ShowElevated(windowHandle, left, top, pixelWidth, pixelHeight);
    }

    private void UpdateElevatedPreview()
    {
        if (previewSurface.IsElevated(windowHandle))
        {
            ShowElevatedPreview();
        }
    }

    private bool TryGetElevatedBounds(out int left, out int top, out int pixelWidth, out int pixelHeight)
    {
        left = 0;
        top = 0;
        pixelWidth = 0;
        pixelHeight = 0;
        XamlRoot? xamlRoot = Host.XamlRoot;

        if (xamlRoot?.Content is not UIElement root || width <= 0 || height <= 0)
        {
            return false;
        }

        try
        {
            Point origin = coordinateHost.TransformToVisual(root).TransformPoint(default);
            double scale = xamlRoot.RasterizationScale;

            if (!double.IsFinite(origin.X) || !double.IsFinite(origin.Y) ||
                !double.IsFinite(scale) || scale <= 0)
            {
                return false;
            }

            left = ToInt32((origin.X + x + dragHorizontalDelta) * scale);
            top = ToInt32((origin.Y + y + dragVerticalDelta) * scale);
            pixelWidth = Math.Max(1, ToInt32(width * scale));
            pixelHeight = Math.Max(1, ToInt32(height * scale));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void ReleasePromotion()
    {
        if (!previewSurface.IsElevated(windowHandle))
        {
            return;
        }

        previewSurface.HideElevated(windowHandle);
        Canvas.SetZIndex(Host, zIndex);
        Canvas.SetZIndex(focusHost, zIndex);
        PromotionReleased?.Invoke(windowHandle);
    }

    private static int ToInt32(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);
    }

    private void ApplyInteractionState() => Host.IsHitTestVisible = interactionEnabled && isFilterMatch;
}
