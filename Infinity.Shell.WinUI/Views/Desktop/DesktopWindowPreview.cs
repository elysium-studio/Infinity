using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;
using Windows.Foundation;

namespace Infinity.Shell.WinUI;

internal readonly record struct DesktopWindowSnapTarget(DesktopSnapPlacement Placement);

internal sealed class DesktopWindowPreview :
    IDisposable
{
    private const float ShadowDepth = 40;
    private const double DragThreshold = 4;
    private const int DraggedZIndex = 1_000_000;
    private const int DraggedPageZIndex = 999_000;

    private readonly ThumbnailCompositionPreview? preview;
    private readonly Border focusHost;
    private readonly Grid focusVisual;
    private readonly ITrackedWindowDragController dragController;
    private readonly IWindowNavigationCoordinator windowNavigationCoordinator;
    private readonly DesktopOverviewDragScroller overviewDragScroller;
    private readonly DesktopWindowDragPositionResolver dragPositionResolver;
    private readonly DesktopDragBoundaryCalculator dragBoundaryCalculator;
    private readonly DesktopDragCursorConfinement cursorConfinement;
    private readonly nint windowHandle;
    private readonly double layoutScale;
    private readonly float shadowDepth;
    private uint? dragPointerId;
    private Point dragStartPoint;
    private Point dragLastPoint;
    private UIElement? dragCoordinateRoot;
    private double dragHorizontalDelta;
    private double dragVerticalDelta;
    private double x;
    private double y;
    private double width;
    private double height;
    private DesktopWindowSnapTarget? snapTarget;
    private bool interactionEnabled;
    private bool isFilterMatch = true;
    private bool isDragging;
    private bool isPagePromoted;
    private bool isPromoted;
    private bool suppressNextTap;
    private bool disposed;
    private int zIndex;

    public DesktopWindowPreview(nint windowHandle, Border host, Border focusHost, ThumbnailCompositionPreview? preview, Grid focusVisual, ITrackedWindowDragController dragController, IWindowNavigationCoordinator windowNavigationCoordinator, DesktopOverviewDragScroller overviewDragScroller, DesktopWindowDragPositionResolver dragPositionResolver, DesktopDragBoundaryCalculator dragBoundaryCalculator, DesktopDragCursorConfinement cursorConfinement, double layoutScale)
    {
        this.windowHandle = windowHandle;
        Host = host;
        this.focusHost = focusHost;
        this.preview = preview;
        this.focusVisual = focusVisual;
        this.dragController = dragController;
        this.windowNavigationCoordinator = windowNavigationCoordinator;
        this.overviewDragScroller = overviewDragScroller;
        this.dragPositionResolver = dragPositionResolver;
        this.dragBoundaryCalculator = dragBoundaryCalculator;
        this.cursorConfinement = cursorConfinement;
        this.layoutScale = double.IsFinite(layoutScale) && layoutScale > 0 ? layoutScale : 1;
        shadowDepth = ToFloat(ShadowDepth / this.layoutScale);

        Host.PointerPressed += HandlePointerPressed;
        Host.PointerMoved += HandlePointerMoved;
        Host.PointerReleased += HandlePointerReleased;
        Host.PointerCanceled += HandlePointerCanceled;
        Host.PointerCaptureLost += HandlePointerCaptureLost;
        Host.Tapped += HandleTapped;
    }

    public event Action<nint>? Invoked;

    public event Action<nint>? PositionChanged;

    public event Action<nint>? Foregrounded;

    public event Action<nint, double, double>? DragMoved;

    public event Action<nint>? DragCompleted;

    public Border Host { get; }

    public Border FocusHost => focusHost;

    public int ZIndex => zIndex;

    public double SourceWidth { get; private set; }

    public double SourceHeight { get; private set; }

    public void RefreshSourceSize(TrackedWindow trackedWindow, IWindowGeometryReader geometryReader)
    {
        double previousWidth = SourceWidth;
        double previousHeight = SourceHeight;

        if (geometryReader.TryReadVisibleGeometry(trackedWindow.Handle, out _, out _, out int visibleWidth, out int visibleHeight))
        {
            SourceWidth = visibleWidth;
            SourceHeight = visibleHeight;
        }
        else
        {
            SourceWidth = trackedWindow.Width;
            SourceHeight = trackedWindow.Height;
        }

        if (previousWidth > 0 && previousHeight > 0 && (previousWidth != SourceWidth || previousHeight != SourceHeight))
        {
            preview?.RefreshSource();
        }
    }

    public void SetZIndex(int value)
    {
        zIndex = value;
        ApplyZIndex();
    }

    public void SetPagePromoted(bool value)
    {
        if (isPagePromoted == value)
        {
            return;
        }

        isPagePromoted = value;
        ApplyZIndex();
    }

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

        double opacity = value ? 1 : 0;
        Host.Opacity = opacity;
        focusHost.Opacity = opacity;
        ApplyInteractionState();
    }

    public void SetSelected(bool value) => focusVisual.Visibility = value ? Visibility.Visible : Visibility.Collapsed;

    public void Activate()
    {
        windowNavigationCoordinator.Activate(windowHandle);
        Foregrounded?.Invoke(windowHandle);
    }

    public void Update(double x, double y, double width, double height, TimeSpan? transitionDuration = null)
    {
        Host.TranslationTransition = transitionDuration.HasValue ? new Vector3Transition { Duration = transitionDuration.Value } : null;
        focusHost.TranslationTransition = transitionDuration.HasValue ? new Vector3Transition { Duration = transitionDuration.Value } : null;

        if (isDragging)
        {
            dragHorizontalDelta += this.x - x;
            dragVerticalDelta += this.y - y;

            ReconcilePointerBoundary();
        }

        this.x = x;
        this.y = y;
        ApplyTranslation();

        if (this.width != width || this.height != height)
        {
            this.width = width;
            this.height = height;

            ApplySize(width, height);
        }
    }

    public void SetSnapTarget(DesktopWindowSnapTarget? target)
    {
        snapTarget = target;
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

    private static float ToFloat(double value) => (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);

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
        dragLastPoint = dragStartPoint;

        Activate();
        SetPromoted(true);

        args.Handled = true;
    }

    private void HandlePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId || dragCoordinateRoot is null)
        {
            return;
        }

        Point rawPoint = args.GetCurrentPoint(dragCoordinateRoot).Position;
        double viewportWidth = Host.XamlRoot?.Size.Width ?? 0;
        double viewportHeight = Host.XamlRoot?.Size.Height ?? 0;
        (double pointerX, double pointerY) = dragBoundaryCalculator.Constrain(rawPoint.X, rawPoint.Y, viewportWidth, viewportHeight, layoutScale);
        Point currentPoint = new(pointerX, pointerY);
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
            dragHorizontalDelta = horizontalDelta / layoutScale;
            dragVerticalDelta = verticalDelta / layoutScale;
            dragLastPoint = currentPoint;
            ClearTranslationTransition();
            cursorConfinement.Begin(viewportWidth, viewportHeight, layoutScale, Host.XamlRoot?.RasterizationScale ?? 1, constrainVertical: true);
        }
        else
        {
            dragHorizontalDelta += (currentPoint.X - dragLastPoint.X) / layoutScale;
            dragVerticalDelta += (currentPoint.Y - dragLastPoint.Y) / layoutScale;
            dragLastPoint = currentPoint;
        }

        overviewDragScroller.Update(Host.DispatcherQueue, currentPoint.X, viewportWidth);
        cursorConfinement.Update(viewportWidth, viewportHeight, layoutScale, Host.XamlRoot?.RasterizationScale ?? 1);
        DragMoved?.Invoke(windowHandle, currentPoint.X, currentPoint.Y);
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
        DesktopWindowSnapTarget? completedSnapTarget = snapTarget;

        if (wasDragging)
        {
            overviewDragScroller.Stop();
            cursorConfinement.Release();
        }

        dragPointerId = null;
        dragCoordinateRoot = null;
        dragHorizontalDelta = 0;
        dragVerticalDelta = 0;
        isDragging = false;
        snapTarget = null;

        SetPromoted(false);

        if (wasDragging)
        {
            bool moved = completedSnapTarget.HasValue
                ? dragController.MoveAndResize(windowHandle, completedSnapTarget.Value.Placement.CanvasX, completedSnapTarget.Value.Placement.CanvasY, completedSnapTarget.Value.Placement.Width, completedSnapTarget.Value.Placement.Height)
                : dragPositionResolver.TryResolve(windowHandle, horizontalDelta, verticalDelta, out DesktopWindowDragPosition position) && dragController.MoveTo(windowHandle, position.CanvasX, position.CanvasY);

            dragController.End(windowHandle);

            if (moved)
            {
                PositionChanged?.Invoke(windowHandle);
                DragCompleted?.Invoke(windowHandle);
                return;
            }

            DragCompleted?.Invoke(windowHandle);
        }

        ApplySize(width, height);
        ApplyTranslation();
    }

    private void ReconcilePointerBoundary()
    {
        double viewportWidth = Host.XamlRoot?.Size.Width ?? 0;
        double viewportHeight = Host.XamlRoot?.Size.Height ?? 0;
        (double pointerX, double pointerY) = dragBoundaryCalculator.Constrain(dragLastPoint.X, dragLastPoint.Y, viewportWidth, viewportHeight, layoutScale);

        dragHorizontalDelta += (pointerX - dragLastPoint.X) / layoutScale;
        dragVerticalDelta += (pointerY - dragLastPoint.Y) / layoutScale;
        dragLastPoint = new Point(pointerX, pointerY);
    }

    private void ApplyTranslation()
    {
        Vector3 translation = new(ToFloat(x + dragHorizontalDelta), ToFloat(y + dragVerticalDelta), shadowDepth);

        Host.Translation = translation;
        focusHost.Translation = translation;
    }

    private void ApplySize(double targetWidth, double targetHeight)
    {
        Host.Width = targetWidth;
        Host.Height = targetHeight;
        focusHost.Width = targetWidth;
        focusHost.Height = targetHeight;
        preview?.Update(targetWidth, targetHeight, true);
    }

    private void SetPromoted(bool value)
    {
        if (isPromoted == value)
        {
            return;
        }

        isPromoted = value;
        ApplyZIndex();
    }

    private void ApplyZIndex()
    {
        int valueToApply = isPromoted ? DraggedZIndex : isPagePromoted ? DraggedPageZIndex + Math.Clamp(zIndex, 0, DraggedZIndex - DraggedPageZIndex - 1) : zIndex;

        Canvas.SetZIndex(Host, valueToApply);
        Canvas.SetZIndex(focusHost, valueToApply);
    }

    private void ApplyInteractionState() => Host.IsHitTestVisible = interactionEnabled && isFilterMatch;
}
