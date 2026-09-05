using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;
using Windows.Foundation;
using Windows.System;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopWindowPreview :
    IDisposable
{
    private const float ShadowDepth = 40;
    private const double DragThreshold = 4;
    private const int DraggedZIndex = 1_000_000;
    private const int DraggedPageZIndex = 999_000;

    private readonly ThumbnailCompositionPreview? preview;
    private readonly DesktopThumbnailCaptureVisibility captureVisibility;
    private readonly DesktopWindowPlacementAnimator placementAnimator;
    private DesktopWindowPlacementAnimator.Bounds? placementAnimationSource;
    private bool placementInProgress;
    private readonly Border backgroundHost;
    private readonly Border focusHost;
    private readonly Grid focusVisual;
    private readonly Grid selectionVisual;
    private readonly ITrackedWindowDragController dragController;
    private readonly DesktopWindowDragPageNavigator windowDragPageNavigator;
    private readonly DesktopWindowDragPositionResolver dragPositionResolver;
    private readonly DesktopDragBoundaryCalculator dragBoundaryCalculator;
    private readonly DesktopDragCursorConfinement cursorConfinement;
    private readonly DesktopWindowPlacementCoordinator windowPlacementCoordinator;
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
    private bool isControlClick;
    private bool isFilterMatch = true;
    private bool isDragging;
    private bool isGroupDragLeader;
    private bool isGroupStacked;
    private bool isKeyboardFocused;
    private bool isPagePromoted;
    private bool isPromoted;
    private bool isSelected;
    private bool suppressNextTap;
    private bool disposed;
    private int zIndex;
    private int groupStackIndex;
    private double groupTargetX;
    private double groupTargetY;
    private double heldGroupLeaderX;
    private double heldGroupLeaderY;
    private Vector3? appliedTranslation;
    private int? appliedZIndex;
    private TimeSpan? translationTransitionDuration;
    private TimeSpan? scaleTransitionDuration;

    public DesktopWindowPreview(nint windowHandle, Border host, Border backgroundHost, Border focusHost, ThumbnailCompositionPreview? preview, Grid focusVisual, Grid selectionVisual, ITrackedWindowDragController dragController, DesktopWindowDragPageNavigator windowDragPageNavigator, DesktopWindowDragPositionResolver dragPositionResolver, DesktopDragBoundaryCalculator dragBoundaryCalculator, DesktopDragCursorConfinement cursorConfinement, DesktopWindowPlacementCoordinator windowPlacementCoordinator, DesktopWindowContextMenuBuilder contextMenuBuilder, double layoutScale)
    {
        this.windowHandle = windowHandle;
        Host = host;
        this.backgroundHost = backgroundHost;
        this.focusHost = focusHost;
        placementAnimator = new(host, backgroundHost, focusHost);
        this.preview = preview;
        captureVisibility = new(preview, host.DispatcherQueue);
        this.focusVisual = focusVisual;
        this.selectionVisual = selectionVisual;
        this.dragController = dragController;
        this.windowDragPageNavigator = windowDragPageNavigator;
        this.dragPositionResolver = dragPositionResolver;
        this.dragBoundaryCalculator = dragBoundaryCalculator;
        this.cursorConfinement = cursorConfinement;
        this.windowPlacementCoordinator = windowPlacementCoordinator;
        this.layoutScale = double.IsFinite(layoutScale) && layoutScale > 0 ? layoutScale : 1;
        shadowDepth = ToFloat(ShadowDepth / this.layoutScale);

        Host.PointerPressed += HandlePointerPressed;
        Host.PointerMoved += HandlePointerMoved;
        Host.PointerReleased += HandlePointerReleased;
        Host.PointerCanceled += HandlePointerCanceled;
        Host.PointerCaptureLost += HandlePointerCaptureLost;
        Host.Tapped += HandleTapped;
        Host.ContextFlyout = contextMenuBuilder.Create(windowHandle);
        windowDragPageNavigator.PageSnapCommitted += HandleWindowPageSnapCommitted;
    }

    public event Action<nint>? Invoked;

    public event Action<nint>? SelectionToggled;

    public event Action<nint>? PositionChanged;

    public event Action<nint, double, double>? DragMoved;

    public event Action<nint>? DragStarted;

    public event Action<DesktopWindowDragCompletion>? DragCompleted;

    public Border Host { get; }

    public Border BackgroundHost => backgroundHost;

    public Border FocusHost => focusHost;

    public int ZIndex => zIndex;

    public double SourceWidth { get; private set; }

    public double SourceHeight { get; private set; }

    public double SourceOffsetX { get; private set; }

    public double SourceOffsetY { get; private set; }

    public double VisualX => x + dragHorizontalDelta;

    public double VisualY => y + dragVerticalDelta;

    public double LayoutScale => layoutScale;

    public void SetCaptureViewport(DesktopCaptureViewport viewport) => captureVisibility.SetViewport(viewport);

    public void RefreshSourceGeometry(TrackedWindow trackedWindow, IWindowGeometryReader geometryReader)
    {
        if (geometryReader.TryReadVisibleGeometry(trackedWindow.Handle,
            out int visibleX,
            out int visibleY,
            out int visibleWidth,
            out int visibleHeight))
        {
            SourceWidth = visibleWidth;
            SourceHeight = visibleHeight;

            if (geometryReader.TryReadGeometry(trackedWindow.Handle,
                out int windowX,
                out int windowY,
                out int windowWidth,
                out int windowHeight))
            {
                SourceOffsetX = Math.Clamp(visibleX - windowX,
                    0,
                    Math.Max(0, windowWidth - visibleWidth));
                SourceOffsetY = Math.Clamp(visibleY - windowY,
                    0,
                    Math.Max(0, windowHeight - visibleHeight));
            }
            else
            {
                SourceOffsetX = 0;
                SourceOffsetY = 0;
            }
        }
        else
        {
            SourceWidth = trackedWindow.Width;
            SourceHeight = trackedWindow.Height;
            SourceOffsetX = 0;
            SourceOffsetY = 0;
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
            CancelPlacementAnimation();
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
        backgroundHost.Opacity = opacity;
        focusHost.Opacity = opacity;
        RefreshCaptureVisibility();
        ApplyInteractionState();
    }

    public void SetKeyboardFocused(bool value)
    {
        isKeyboardFocused = value;
        ApplyIndicatorVisibility();
    }

    public void SetSelected(bool value)
    {
        isSelected = value;
        ApplyIndicatorVisibility();
    }

    public void SetGroupDragLeader(bool value)
    {
        isGroupDragLeader = value;

        if (value)
        {
            heldGroupLeaderX = x + dragHorizontalDelta;
            heldGroupLeaderY = y + dragVerticalDelta;
        }

        RefreshCaptureVisibility();
        ApplyIndicatorVisibility();
    }

    public void SetGroupStackTarget(double targetX, double targetY, float scale, int stackIndex, TimeSpan? transitionDuration)
    {
        isGroupStacked = true;
        groupTargetX = targetX;
        groupTargetY = targetY;
        groupStackIndex = Math.Max(1, stackIndex);

        SetGroupTransitions(transitionDuration);

        Vector3 targetScale = new(Math.Clamp(scale, 0.82f, 1), Math.Clamp(scale, 0.82f, 1), 1);
        Host.Scale = targetScale;
        backgroundHost.Scale = targetScale;
        focusHost.Scale = targetScale;

        ApplyTranslation();
        ApplyZIndex();
        ApplyIndicatorVisibility();
        ApplyInteractionState();
    }

    public void ClearGroupDragVisual(TimeSpan? transitionDuration)
    {
        bool wasGroupDragLeader = isGroupDragLeader;
        SetGroupTransitions(transitionDuration);

        isGroupDragLeader = false;
        isGroupStacked = false;
        groupStackIndex = 0;
        Host.Scale = Vector3.One;
        backgroundHost.Scale = Vector3.One;
        focusHost.Scale = Vector3.One;

        if (!wasGroupDragLeader)
        {
            ApplyTranslation();
        }
        ApplyZIndex();
        RefreshCaptureVisibility();
        ApplyIndicatorVisibility();
        ApplyInteractionState();
        StartPendingPlacementAnimation();
    }

    public void BeginPlacementAnimation()
    {
        Vector3 translation = appliedTranslation ?? Host.Translation;
        Vector3 scale = Host.Scale;
        placementAnimationSource = placementAnimator.Capture(new(
            translation.X + width / 2 * (1 - scale.X),
            translation.Y + height / 2 * (1 - scale.Y), width * scale.X, height * scale.Y));
        placementAnimator.Stop();
        if (placementAnimationSource is { IsValid: true } source && width > 0 && height > 0)
        {
            // Hold the displayed bounds while native resize calls run. This
            // also prevents a second arrange flashing the previous destination.
            SetGroupTransitions(null);
            Vector3 heldScale = new(ToFloat(source.Width / width), ToFloat(source.Height / height), 1);
            Vector3 heldTranslation = new(ToFloat(source.X - width / 2 * (1 - heldScale.X)),
                ToFloat(source.Y - height / 2 * (1 - heldScale.Y)), shadowDepth);
            Host.Scale = backgroundHost.Scale = focusHost.Scale = heldScale;
            Host.Translation = backgroundHost.Translation = focusHost.Translation = heldTranslation;
            appliedTranslation = heldTranslation;
        }
        placementInProgress = true;
        captureVisibility.HoldForTransition(DesktopWindowPlacementAnimator.Duration);
    }

    public void EndPlacementAnimation() => placementInProgress = false;

    private void StartPendingPlacementAnimation()
    {
        if (placementInProgress || isGroupDragLeader || isGroupStacked || placementAnimationSource is not { } source) return;
        placementAnimationSource = null;
        SetGroupTransitions(null);
        Host.Scale = backgroundHost.Scale = focusHost.Scale = Vector3.One;
        ApplyTranslation();
        captureVisibility.HoldForTransition(DesktopWindowPlacementAnimator.Duration);
        placementAnimator.Start(source, new(VisualX, VisualY, width, height), shadowDepth);
    }

    private void CancelPlacementAnimation()
    {
        bool wasPending = placementAnimationSource.HasValue;
        placementAnimationSource = null;
        placementInProgress = false;
        placementAnimator.Stop();
        if (wasPending && !isGroupStacked)
        {
            Host.Scale = backgroundHost.Scale = focusHost.Scale = Vector3.One;
            ApplyTranslation();
        }
    }

    public void Update(double x, double y, double width, double height, TimeSpan? transitionDuration = null)
    {
        if (placementInProgress) return;
        if (placementAnimationSource is null && (this.x != x || this.y != y || this.width != width || this.height != height))
        {
            placementAnimator.Stop();
        }
        captureVisibility.HoldForTransition(transitionDuration);
        SetTranslationTransition(transitionDuration);

        if (isDragging)
        {
            dragHorizontalDelta += this.x - x;
            dragVerticalDelta += this.y - y;

            ReconcilePointerBoundary();
        }

        this.x = x;
        this.y = y;
        ApplyTranslation(updateCapture: false);

        if (this.width != width || this.height != height)
        {
            this.width = width;
            this.height = height;

            ApplySize(width, height);
        }
        RefreshCaptureVisibility();
        StartPendingPlacementAnimation();
    }

    public void SetSnapTarget(DesktopWindowSnapTarget? target)
    {
        snapTarget = target;
    }

    public void ClearTranslationTransition()
    {
        SetTranslationTransition(null);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelPlacementAnimation();
        placementAnimator.Dispose();

        CompleteDrag();
        Host.ReleasePointerCaptures();

        Host.PointerPressed -= HandlePointerPressed;
        Host.PointerMoved -= HandlePointerMoved;
        Host.PointerReleased -= HandlePointerReleased;
        Host.PointerCanceled -= HandlePointerCanceled;
        Host.PointerCaptureLost -= HandlePointerCaptureLost;
        Host.Tapped -= HandleTapped;
        windowDragPageNavigator.PageSnapCommitted -= HandleWindowPageSnapCommitted;

        captureVisibility.Dispose();
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

        if (isControlClick)
        {
            isControlClick = false;
            SelectionToggled?.Invoke(windowHandle);
            return;
        }

        Invoked?.Invoke(windowHandle);
    }

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        CancelPlacementAnimation();
        suppressNextTap = false;
        isControlClick = args.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
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
        double horizontalPointerDelta = currentPoint.X - dragLastPoint.X;

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

            Point grabPoint = args.GetCurrentPoint(Host).Position;
            double grabX = width > 0 ? Math.Clamp(grabPoint.X / width, 0, 1) : 0.5;
            double grabY = height > 0 ? Math.Clamp(grabPoint.Y / height, 0, 1) : 0.5;
            double previousX = x;
            double previousY = y;
            isDragging = true;
            if (!windowPlacementCoordinator.TryPrepareForMove(windowHandle, out DesktopSnapPlacement restored, out DesktopSnapPlacement original))
            {
                isDragging = false;
                suppressNextTap = true;
                dragController.End(windowHandle);
                CompleteDrag();
                Host.ReleasePointerCapture(args.Pointer);
                return;
            }
            isControlClick = false;
            suppressNextTap = true;
            dragHorizontalDelta = horizontalDelta / layoutScale;
            dragVerticalDelta = verticalDelta / layoutScale;
            if (restored != original)
            {
                // Preserve the point held by the pointer as the maximised
                // thumbnail shrinks, including a drop before the next layout.
                x = previousX + restored.CanvasX - original.CanvasX;
                y = previousY + restored.CanvasY - original.CanvasY;
                dragHorizontalDelta += original.CanvasX - restored.CanvasX + (original.Width - restored.Width) * grabX;
                dragVerticalDelta += original.CanvasY - restored.CanvasY + (original.Height - restored.Height) * grabY;
                width = restored.Width;
                height = restored.Height;
                ApplySize(width, height);
            }
            dragLastPoint = currentPoint;
            ClearTranslationTransition();
            ApplyIndicatorVisibility();
            DragStarted?.Invoke(windowHandle);
            DesktopDragBounds centeredPageBounds = dragBoundaryCalculator.GetCenteredPageBounds(viewportWidth, viewportHeight, layoutScale);
            bool startsWithinCenteredPage = windowDragPageNavigator.IsEnabled &&
                centeredPageBounds.IsValid &&
                currentPoint.X >= centeredPageBounds.MinimumX &&
                currentPoint.X <= centeredPageBounds.MaximumX;
            cursorConfinement.Begin(viewportWidth, viewportHeight, layoutScale, Host.XamlRoot?.RasterizationScale ?? 1, constrainVertical: true, constrainToCenteredPage: startsWithinCenteredPage);
        }
        else
        {
            dragHorizontalDelta += (currentPoint.X - dragLastPoint.X) / layoutScale;
            dragVerticalDelta += (currentPoint.Y - dragLastPoint.Y) / layoutScale;
            dragLastPoint = currentPoint;
        }

        windowDragPageNavigator.Update(Host.DispatcherQueue, currentPoint.X, horizontalPointerDelta, viewportWidth, layoutScale);
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

        isControlClick = false;
        CompleteDrag();
        Host.ReleasePointerCapture(args.Pointer);
        args.Handled = true;
    }

    private void HandlePointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId == args.Pointer.PointerId)
        {
            isControlClick = false;
            CompleteDrag();
        }
    }

    private void CompleteDrag()
    {
        bool wasDragging = isDragging;
        bool wasGroupDrag = wasDragging && isGroupDragLeader;
        double horizontalDelta = dragHorizontalDelta;
        double verticalDelta = dragVerticalDelta;
        DesktopWindowSnapTarget? completedSnapTarget = snapTarget;

        if (wasDragging)
        {
            windowDragPageNavigator.Stop();
            cursorConfinement.Release();
        }

        dragPointerId = null;
        dragCoordinateRoot = null;
        if (wasGroupDrag)
        {
            heldGroupLeaderX = x + dragHorizontalDelta;
            heldGroupLeaderY = y + dragVerticalDelta;
        }

        isDragging = false;
        ApplyIndicatorVisibility();
        snapTarget = null;

        if (wasDragging)
        {
            if (wasGroupDrag)
            {
                DragCompleted?.Invoke(new DesktopWindowDragCompletion(windowHandle, horizontalDelta, verticalDelta, completedSnapTarget, true, false));
                dragController.End(windowHandle);
                dragHorizontalDelta = 0;
                dragVerticalDelta = 0;
                isGroupDragLeader = false;
                SetPromoted(false);
                ApplySize(width, height);
                ApplyTranslation();
                ApplyIndicatorVisibility();
                return;
            }

            dragHorizontalDelta = 0;
            dragVerticalDelta = 0;
            SetPromoted(false);

            windowPlacementCoordinator.CompleteMove(windowHandle);
            bool moved = completedSnapTarget is { OccupantHandle: not 0 } swapTarget
                ? windowPlacementCoordinator.TrySwapIntoSlot(windowHandle, swapTarget.OccupantHandle, swapTarget.Placement)
                : completedSnapTarget.HasValue
                ? dragController.MoveAndResize(windowHandle, completedSnapTarget.Value.Placement.CanvasX, completedSnapTarget.Value.Placement.CanvasY, completedSnapTarget.Value.Placement.Width, completedSnapTarget.Value.Placement.Height)
                : dragPositionResolver.TryResolve(windowHandle, horizontalDelta, verticalDelta, out DesktopWindowDragPosition position) && dragController.MoveTo(windowHandle, position.CanvasX, position.CanvasY);

            dragController.End(windowHandle);

            if (moved)
            {
                PositionChanged?.Invoke(windowHandle);
                DragCompleted?.Invoke(new DesktopWindowDragCompletion(windowHandle, horizontalDelta, verticalDelta, completedSnapTarget, false, true));
                return;
            }

            DragCompleted?.Invoke(new DesktopWindowDragCompletion(windowHandle, horizontalDelta, verticalDelta, completedSnapTarget, false, false));
        }
        else
        {
            dragHorizontalDelta = 0;
            dragVerticalDelta = 0;
            SetPromoted(false);
        }

        ApplySize(width, height);
        ApplyTranslation();
    }

    private void ReconcilePointerBoundary()
    {
        double viewportWidth = Host.XamlRoot?.Size.Width ?? 0;
        double viewportHeight = Host.XamlRoot?.Size.Height ?? 0;
        (double pointerX, double pointerY) = cursorConfinement.IsConstrainedToCenteredPage
            ? dragBoundaryCalculator.ConstrainToCenteredPage(dragLastPoint.X, dragLastPoint.Y, viewportWidth, viewportHeight, layoutScale)
            : dragBoundaryCalculator.Constrain(dragLastPoint.X, dragLastPoint.Y, viewportWidth, viewportHeight, layoutScale);

        dragHorizontalDelta += (pointerX - dragLastPoint.X) / layoutScale;
        dragVerticalDelta += (pointerY - dragLastPoint.Y) / layoutScale;
        dragLastPoint = new Point(pointerX, pointerY);
    }

    private void HandleWindowPageSnapCommitted()
    {
        if (isDragging)
        {
            cursorConfinement.UseCenteredPageBounds();
        }
    }

    private void ApplyTranslation(bool updateCapture = true)
    {
        double targetX = isGroupStacked ? groupTargetX : isGroupDragLeader && !isDragging ? heldGroupLeaderX : x + dragHorizontalDelta;
        double targetY = isGroupStacked ? groupTargetY : isGroupDragLeader && !isDragging ? heldGroupLeaderY : y + dragVerticalDelta;
        Vector3 translation = new(ToFloat(targetX), ToFloat(targetY), shadowDepth);

        if (appliedTranslation != translation)
        {
            appliedTranslation = translation;
            Host.Translation = translation;
            backgroundHost.Translation = translation;
            focusHost.Translation = translation;
        }
        if (updateCapture) RefreshCaptureVisibility();
    }

    private void RefreshCaptureVisibility() => captureVisibility.Update(
        appliedTranslation?.X ?? 0, appliedTranslation?.Y ?? 0, width, height, isFilterMatch,
        isDragging || isGroupDragLeader || isGroupStacked);

    private void ApplySize(double targetWidth, double targetHeight)
    {
        Host.Width = targetWidth;
        Host.Height = targetHeight;
        backgroundHost.Width = targetWidth;
        backgroundHost.Height = targetHeight;
        focusHost.Width = targetWidth;
        focusHost.Height = targetHeight;
        Host.CenterPoint = new Vector3(ToFloat(targetWidth / 2), ToFloat(targetHeight / 2), 0);
        backgroundHost.CenterPoint = Host.CenterPoint;
        focusHost.CenterPoint = Host.CenterPoint;
        RefreshCaptureVisibility();
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
        int valueToApply = isPromoted
            ? DraggedZIndex
            : isGroupStacked
                ? DraggedZIndex - Math.Clamp(groupStackIndex, 1, 1000)
                : isPagePromoted
                    ? DraggedPageZIndex + Math.Clamp(zIndex, 0, DraggedZIndex - DraggedPageZIndex - 1)
                    : zIndex;

        if (appliedZIndex == valueToApply) return;
        appliedZIndex = valueToApply;
        Canvas.SetZIndex(Host, valueToApply);
        Canvas.SetZIndex(backgroundHost, valueToApply);
        Canvas.SetZIndex(focusHost, valueToApply);
    }

    private void ApplyInteractionState() => Host.IsHitTestVisible = interactionEnabled && isFilterMatch && !isGroupStacked;

    private void ApplyIndicatorVisibility()
    {
        bool groupDragging = isGroupDragLeader || isGroupStacked;
        focusVisual.Visibility = isKeyboardFocused && !isSelected && !isDragging && !groupDragging ? Visibility.Visible : Visibility.Collapsed;
        selectionVisual.Visibility = isSelected && !isDragging && !groupDragging ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetGroupTransitions(TimeSpan? duration)
    {
        captureVisibility.HoldForTransition(duration);
        SetTranslationTransition(duration);
        if (scaleTransitionDuration == duration) return;
        scaleTransitionDuration = duration;
        Host.ScaleTransition = duration.HasValue ? new Vector3Transition { Duration = duration.Value } : null;
        backgroundHost.ScaleTransition = duration.HasValue ? new Vector3Transition { Duration = duration.Value } : null;
        focusHost.ScaleTransition = duration.HasValue ? new Vector3Transition { Duration = duration.Value } : null;
    }

    private void SetTranslationTransition(TimeSpan? duration)
    {
        if (translationTransitionDuration == duration) return;
        translationTransitionDuration = duration;
        Host.TranslationTransition = duration.HasValue ? new Vector3Transition { Duration = duration.Value } : null;
        backgroundHost.TranslationTransition = duration.HasValue ? new Vector3Transition { Duration = duration.Value } : null;
        focusHost.TranslationTransition = duration.HasValue ? new Vector3Transition { Duration = duration.Value } : null;
    }
}
