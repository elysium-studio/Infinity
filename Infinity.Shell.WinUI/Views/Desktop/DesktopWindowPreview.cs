using System;
using System.Diagnostics;
using System.Numerics;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopWindowPreview : IDisposable
{
    private const float ShadowDepth = 40;
    private const double DragThreshold = 4;
    private const int DraggedZIndex = 1_000_000;
    private const int DraggedPageZIndex = 999_000;
    private readonly ThumbnailCompositionPreview? preview;
    private readonly Grid liveContent;
    private readonly Func<DesktopWindowResizePreview> createResizePreview;
    private DesktopWindowResizePreview? resizePreview;
    private readonly DesktopThumbnailCaptureVisibility captureVisibility;
    private readonly DesktopWindowPlacementAnimator placementAnimator;
    private DesktopWindowPlacementAnimator.Bounds? placementAnimationSource;
    private bool placementInProgress;
    private bool boundaryResizePreview;
    private DesktopWindowDragAnchorVisual? dragVisual;
    private bool isLiveDragging;
    private readonly DesktopWindowDragFrames dragFrames;
    private readonly IPager pager;
    private readonly DesktopWindowThrowGesture throwGesture = new();
    private readonly long throwClockOrigin = Stopwatch.GetTimestamp();
    private int? throwOriginPage;
    private DesktopSnapPlacement? throwOriginPlacement;
    private readonly DesktopWindowThrowPlacementResolver throwPlacementResolver;
    private readonly PageLayoutStore pageLayouts;
    private Vector2 dragGrabOffset;
    private int sourceWindowWidth;
    private int sourceWindowHeight;
    private readonly Border backgroundHost;
    private readonly Border[] presentationElements;
    private readonly Border focusHost;
    private readonly Grid focusVisual;
    private readonly Grid selectionVisual;
    private readonly ITrackedWindowDragController dragController;
    private readonly DesktopOverviewDragScroller overviewDragScroller;
    private readonly DesktopWindowDragPositionResolver dragPositionResolver;
    private readonly DesktopDragBoundaryCalculator dragBoundaryCalculator;
    private readonly DesktopDragCursorConfinement cursorConfinement;
    private readonly DesktopWindowPlacementCoordinator windowPlacementCoordinator;
    private readonly nint windowHandle;
    private readonly double layoutScale;
    private readonly float shadowDepth;
    private readonly CornerRadius floatingCornerRadius;
    private bool? isSlotted;
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
    private int? dropPage;
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

    public DesktopWindowPreview(nint windowHandle, Border host, Border backgroundHost, Border focusHost, ThumbnailCompositionPreview? preview, Grid liveContent, Func<DesktopWindowResizePreview> createResizePreview, Grid focusVisual, Grid selectionVisual, ITrackedWindowDragController dragController, DesktopOverviewDragScroller overviewDragScroller, DesktopWindowDragPositionResolver dragPositionResolver, DesktopDragBoundaryCalculator dragBoundaryCalculator, DesktopDragCursorConfinement cursorConfinement, DesktopWindowPlacementCoordinator windowPlacementCoordinator, DesktopWindowContextMenuBuilder contextMenuBuilder, DesktopWindowDragFrames dragFrames, IPager pager, DesktopWindowThrowPlacementResolver throwPlacementResolver, PageLayoutStore pageLayouts, double layoutScale)
    {
        this.windowHandle = windowHandle;
        Host = host;
        floatingCornerRadius = host.CornerRadius;
        this.backgroundHost = backgroundHost;
        this.focusHost = focusHost;
        presentationElements = [host, backgroundHost, focusHost];
        placementAnimator = new(host, backgroundHost, focusHost);
        this.preview = preview;
        this.liveContent = liveContent;
        this.createResizePreview = createResizePreview;
        captureVisibility = new(preview, host.DispatcherQueue);
        this.focusVisual = focusVisual;
        this.selectionVisual = selectionVisual;
        this.dragController = dragController;
        this.dragFrames = dragFrames;
        this.pager = pager;
        this.throwPlacementResolver = throwPlacementResolver;
        this.pageLayouts = pageLayouts;
        this.overviewDragScroller = overviewDragScroller;
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

    public void UpdateDragScroll(Point pointer) => overviewDragScroller.UpdateWindowDrag(pointer.X, Host.XamlRoot?.Size.Width ?? 0, layoutScale);

    public void StopDragScroll() => overviewDragScroller.Stop();

    public bool IsDragging => isDragging || isLiveDragging || isGroupDragLeader || isGroupStacked;

    public bool CanDeferLiveDragRefresh(TrackedWindow window) => isLiveDragging && sourceWindowWidth == window.Width && sourceWindowHeight == window.Height;

    public void SetLiveDragAnchor(Point pointer, DesktopWindowDragAnchor anchor, bool followAnimatedScale)
    {
        if (!isLiveDragging)
        {
            BeginThrowGesture();
            CancelPlacementAnimation();
            SetGroupTransitions(null);
            SetPromoted(true);
        }

        isLiveDragging = true;
        if (followAnimatedScale)
        {
            throwGesture.Reset();
        }
        else
        {
            RecordThrowPointer(pointer);
        }
        (double grabX, double grabY) = anchor.GetOffset(SourceWidth, SourceHeight);
        UpdateDragVisual(pointer, new(ToFloat(grabX), ToFloat(grabY)), followAnimatedScale);
        ApplyIndicatorVisibility();
    }

    private void UpdateDragVisual(Point rootPointer, Vector2 grabOffset, bool followAnimatedScale = false)
    {
        if (!followAnimatedScale)
        {
            if (dragVisual is not null)
            {
                dragVisual.Dispose();
                dragVisual = null;
                appliedTranslation = null;
            }

            (double layoutX, double layoutY) = dragFrames.Viewport.ToLayout(rootPointer.X, rootPointer.Y, layoutScale);
            ApplyPosition(layoutX - grabOffset.X, layoutY - grabOffset.Y);
            RefreshCaptureVisibility();
            return;
        }

        if (dragFrames.Surface is not { } surface)
        {
            return;
        }

        (double pointerX, double pointerY) = dragFrames.Viewport.ToSurface(rootPointer.X, rootPointer.Y);
        Vector2 pointer = new(ToFloat(pointerX), ToFloat(pointerY));
        if (dragVisual is null)
        {
            CancelPlacementAnimation();
            SetGroupTransitions(null);
            dragVisual = new(surface, pointer, grabOffset, shadowDepth, presentationElements);
            SetPromoted(true);
            ApplyIndicatorVisibility();
            RefreshCaptureVisibility();
        }
        else
        {
            dragVisual.Update(pointer, grabOffset);
        }
    }

    public void ClearDragVisual()
    {
        bool wasLiveDragging = isLiveDragging;
        isLiveDragging = false;
        if (dragVisual is null && !wasLiveDragging)
        {
            return;
        }

        dragVisual?.Dispose();
        dragVisual = null;
        appliedTranslation = null;
        SetPromoted(false);
        ApplyTranslation();
        ApplyIndicatorVisibility();
    }

    private void BeginThrowGesture()
    {
        throwGesture.Reset();
        throwOriginPage = windowPlacementCoordinator.GetPage(windowHandle);
        throwOriginPlacement = throwPlacementResolver.GetOrigin(windowHandle);
    }

    private void RecordThrowPointer(Point pointer) => throwGesture.Update(pointer.X, pointer.Y, Stopwatch.GetElapsedTime(throwClockOrigin));

    public void SetThrowOrigin(int page, DesktopSnapPlacement placement)
    {
        throwOriginPage = page;
        throwOriginPlacement = placement;
    }

    public int ReleaseThrowGesture() => throwGesture.Release(Stopwatch.GetElapsedTime(throwClockOrigin));

    private bool TryGetThrowPage(int direction, out int page)
    {
        page = 0;
        return throwOriginPage.HasValue && DesktopWindowThrowGesture.TryGetTargetPage(throwOriginPage.Value, direction, pager.MaxPages, out page);
    }

    public bool TryThrowLiveWindow(int direction, out int page)
    {
        BeginPlacementAnimation();
        ClearDragVisual();
        windowPlacementCoordinator.CompleteMove(windowHandle);
        bool moved = CompleteThrow(direction, out page);
        EndPlacementAnimation();
        if (!moved)
        {
            CancelPlacementAnimation();
        }

        return moved;
    }

    private bool CompleteThrow(int direction, out int page)
    {
        page = throwOriginPage ?? -1;
        if (!throwOriginPage.HasValue || !throwOriginPlacement.HasValue)
        {
            return false;
        }

        if (TryGetThrowPage(direction, out int targetPage) && throwPlacementResolver.TryResolve(windowHandle, throwOriginPage.Value, throwOriginPlacement.Value, targetPage, pageLayouts.GetLayout(targetPage), out DesktopSnapPlacement placement) && windowPlacementCoordinator.TryPlaceInSlot(windowHandle, placement))
        {
            page = targetPage;
            return true;
        }

        return windowPlacementCoordinator.TryPlaceInSlot(windowHandle, throwOriginPlacement.Value);
    }

    public bool TryPlaceLiveDragOnPage(int page, double localPointerX, double localPointerY, DesktopWindowDragAnchor anchor)
    {
        (double grabX, double grabY) = anchor.GetOffset(SourceWidth, SourceHeight);
        if (!dragPositionResolver.TryResolveOnPage(windowHandle, page, localPointerX - grabX - SourceOffsetX, localPointerY - grabY - SourceOffsetY, out DesktopWindowDragPosition position))
        {
            return false;
        }

        ClearDragVisual();
        windowPlacementCoordinator.CompleteMove(windowHandle);
        return dragController.MoveTo(windowHandle, position.CanvasX, position.CanvasY);
    }

    public void SetBoundaryResizePreview(double deltaX, double deltaY, double targetWidth, double targetHeight)
    {
        if (width <= 0 || height <= 0 || !double.IsFinite(targetWidth) || !double.IsFinite(targetHeight) || targetWidth <= 0 || targetHeight <= 0)
        {
            return;
        }

        boundaryResizePreview = true;
        placementAnimator.Stop();
        SetGroupTransitions(null);
        liveContent.Opacity = 0;
        if (resizePreview is null)
        {
            resizePreview = createResizePreview();
            ((Grid)Host.Child).Children.Add(resizePreview);
        }

        ApplyCornerRadius();
        resizePreview.Show(Host.CornerRadius);
        Vector3 translation = new(ToFloat(x + deltaX), ToFloat(y + deltaY), shadowDepth);
        foreach (Border element in presentationElements)
        {
            element.Width = targetWidth;
            element.Height = targetHeight;
            element.CenterPoint = new(ToFloat(targetWidth / 2), ToFloat(targetHeight / 2), 0);
            element.Scale = Vector3.One;
            element.Translation = translation;
        }
    }

    public void ClearBoundaryResizePreview()
    {
        if (!boundaryResizePreview)
        {
            return;
        }

        boundaryResizePreview = false;
        resizePreview?.Hide();
        ApplyCornerRadius();
        liveContent.Opacity = 1;
        ApplySize(width, height);
        appliedTranslation = null;
        ApplyTranslation();
    }

    public void SetCaptureViewport(DesktopCaptureViewport viewport) => captureVisibility.SetViewport(viewport);

    public void SetIsSlotted(bool value)
    {
        if (isSlotted == value)
        {
            return;
        }

        isSlotted = value;
        ApplyCornerRadius();
        preview?.SetSquareCorners(value);
    }

    private void ApplyCornerRadius()
    {
        CornerRadius radius = boundaryResizePreview ? new(DesktopPagePreview.VisibleCornerRadius / layoutScale) : isSlotted == true ? new(0) : floatingCornerRadius;
        Host.CornerRadius = radius;
        backgroundHost.CornerRadius = radius;
    }


    public void RefreshSourceGeometry(TrackedWindow trackedWindow, IWindowGeometryReader geometryReader)
    {
        if (CanDeferLiveDragRefresh(trackedWindow))
        {
            return;
        }

        sourceWindowWidth = trackedWindow.Width;
        sourceWindowHeight = trackedWindow.Height;
        if (geometryReader.TryReadVisibleGeometry(trackedWindow.Handle, out int visibleX, out int visibleY, out int visibleWidth, out int visibleHeight))
        {
            SourceWidth = visibleWidth;
            SourceHeight = visibleHeight;
            if (geometryReader.TryReadGeometry(trackedWindow.Handle, out int windowX, out int windowY, out int windowWidth, out int windowHeight))
            {
                SourceOffsetX = Math.Clamp(visibleX - windowX, 0, Math.Max(0, windowWidth - visibleWidth));
                SourceOffsetY = Math.Clamp(visibleY - windowY, 0, Math.Max(0, windowHeight - visibleHeight));
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
        placementAnimationSource = placementAnimator.Capture(new(translation.X + width / 2 * (1 - scale.X), translation.Y + height / 2 * (1 - scale.Y), width * scale.X, height * scale.Y));
        placementAnimator.Stop();
        if (placementAnimationSource is { IsValid: true } source && width > 0 && height > 0)
        {
            SetGroupTransitions(null);
            Vector3 heldScale = new(ToFloat(source.Width / width), ToFloat(source.Height / height), 1);
            Vector3 heldTranslation = new(ToFloat(source.X - width / 2 * (1 - heldScale.X)), ToFloat(source.Y - height / 2 * (1 - heldScale.Y)), shadowDepth);
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
        if (placementInProgress || isGroupDragLeader || isGroupStacked || placementAnimationSource is not { } source)
        {
            return;
        }

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
        if (placementInProgress || boundaryResizePreview)
        {
            return;
        }

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


    public void SetSnapTarget(DesktopWindowSnapTarget? target) => snapTarget = target;

    public void SetDropPage(int? page) => dropPage = page;

    public void ClearTranslationTransition() => SetTranslationTransition(null);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ClearDragVisual();
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
        PointerPoint point = args.GetCurrentPoint(Host);
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
        BeginThrowGesture();
        RecordThrowPointer(dragStartPoint);
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
        if (isDragging)
        {
            UpdateDragPosition(rawPoint);
            args.Handled = true;
            return;
        }

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
            cursorConfinement.Begin(viewportWidth, viewportHeight, layoutScale, Host.XamlRoot?.RasterizationScale ?? 1, constrainVertical: true);
            (double layoutX, double layoutY) = dragFrames.Viewport.ToLayout(currentPoint.X, currentPoint.Y, layoutScale);
            dragGrabOffset = new(ToFloat(layoutX - VisualX), ToFloat(layoutY - VisualY));
        }

        UpdateDragPosition(rawPoint);
        args.Handled = true;
    }

    private void UpdateDragPosition(Point rawPoint)
    {
        RecordThrowPointer(rawPoint);
        double viewportWidth = Host.XamlRoot?.Size.Width ?? 0;
        double viewportHeight = Host.XamlRoot?.Size.Height ?? 0;
        (double pointerX, double pointerY) = dragBoundaryCalculator.Constrain(rawPoint.X, rawPoint.Y, viewportWidth, viewportHeight, layoutScale);
        Point currentPoint = new(pointerX, pointerY);
        (double layoutX, double layoutY) = dragFrames.Viewport.ToLayout(pointerX, pointerY, layoutScale);
        dragHorizontalDelta = layoutX - dragGrabOffset.X - x;
        dragVerticalDelta = layoutY - dragGrabOffset.Y - y;
        dragLastPoint = currentPoint;
        UpdateDragScroll(rawPoint);
        cursorConfinement.Update(viewportWidth, viewportHeight, layoutScale, Host.XamlRoot?.RasterizationScale ?? 1);
        DragMoved?.Invoke(windowHandle, currentPoint.X, currentPoint.Y);
        UpdateDragVisual(currentPoint, dragGrabOffset);
    }


    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        bool wasDragging = isDragging;
        if (wasDragging && dragCoordinateRoot is not null)
        {
            UpdateDragPosition(args.GetCurrentPoint(dragCoordinateRoot).Position);
        }

        CompleteDrag(wasDragging ? ReleaseThrowGesture() : 0);
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


    private void CompleteDrag(int throwDirection = 0)
    {
        bool wasDragging = isDragging;
        bool wasGroupDrag = wasDragging && isGroupDragLeader;
        double horizontalDelta = dragHorizontalDelta;
        double verticalDelta = dragVerticalDelta;
        DesktopWindowSnapTarget? completedSnapTarget = snapTarget;
        int? completedDropPage = dropPage;
        bool isThrow = wasDragging && !wasGroupDrag && throwDirection != 0;
        throwGesture.Reset();
        if (isThrow)
        {
            completedDropPage = throwOriginPage;
            completedSnapTarget = null;
        }
        if (wasDragging)
        {
            ClearDragVisual();
            StopDragScroll();
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
        dropPage = null;
        if (wasDragging)
        {
            if (wasGroupDrag)
            {
                DragCompleted?.Invoke(new DesktopWindowDragCompletion(windowHandle, horizontalDelta, verticalDelta, completedSnapTarget, true, false, completedDropPage));
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
            bool moved;
            if (isThrow)
            {
                moved = CompleteThrow(throwDirection, out int destinationPage);
                completedDropPage = destinationPage >= 0 ? destinationPage : null;
            }
            else
            {
                moved = completedSnapTarget is { OccupantHandle: not 0 } swapTarget ? windowPlacementCoordinator.TrySwapIntoSlot(windowHandle, swapTarget.OccupantHandle, swapTarget.Placement) : completedSnapTarget.HasValue ? windowPlacementCoordinator.TryPlaceInSlot(windowHandle, completedSnapTarget.Value.Placement) : dragPositionResolver.TryResolve(windowHandle, horizontalDelta, verticalDelta, out DesktopWindowDragPosition position, completedDropPage) && dragController.MoveTo(windowHandle, position.CanvasX, position.CanvasY);
            }
            dragController.End(windowHandle);
            if (moved)
            {
                PositionChanged?.Invoke(windowHandle);
                DragCompleted?.Invoke(new DesktopWindowDragCompletion(windowHandle, horizontalDelta, verticalDelta, completedSnapTarget, false, true, completedDropPage));
                return;
            }

            DragCompleted?.Invoke(new DesktopWindowDragCompletion(windowHandle, horizontalDelta, verticalDelta, completedSnapTarget, false, false, completedDropPage));
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
        (double pointerX, double pointerY) = dragBoundaryCalculator.Constrain(dragLastPoint.X, dragLastPoint.Y, viewportWidth, viewportHeight, layoutScale);
        dragHorizontalDelta += (pointerX - dragLastPoint.X) / layoutScale;
        dragVerticalDelta += (pointerY - dragLastPoint.Y) / layoutScale;
        dragLastPoint = new(pointerX, pointerY);
    }


    private void ApplyTranslation(bool updateCapture = true)
    {
        if (isLiveDragging || dragVisual is not null)
        {
            return;
        }

        double targetX = isGroupStacked ? groupTargetX : isGroupDragLeader && !isDragging ? heldGroupLeaderX : x + dragHorizontalDelta;
        double targetY = isGroupStacked ? groupTargetY : isGroupDragLeader && !isDragging ? heldGroupLeaderY : y + dragVerticalDelta;
        ApplyPosition(targetX, targetY);
        if (updateCapture)
        {
            RefreshCaptureVisibility();
        }
    }

    private void ApplyPosition(double targetX, double targetY)
    {
        Vector3 translation = new(ToFloat(targetX), ToFloat(targetY), shadowDepth);
        if (appliedTranslation != translation)
        {
            appliedTranslation = translation;
            Host.Translation = translation;
            backgroundHost.Translation = translation;
            focusHost.Translation = translation;
        }

    }


    private void RefreshCaptureVisibility() => captureVisibility.Update(appliedTranslation?.X ?? 0, appliedTranslation?.Y ?? 0, width, height, isFilterMatch, IsDragging);

    private void ApplySize(double targetWidth, double targetHeight)
    {
        Host.Width = targetWidth;
        Host.Height = targetHeight;
        backgroundHost.Width = targetWidth;
        backgroundHost.Height = targetHeight;
        focusHost.Width = targetWidth;
        focusHost.Height = targetHeight;
        Host.CenterPoint = new(ToFloat(targetWidth / 2), ToFloat(targetHeight / 2), 0);
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
        int valueToApply = isPromoted ? DraggedZIndex : isGroupStacked ? DraggedZIndex - Math.Clamp(groupStackIndex, 1, 1000) : isPagePromoted ? DraggedPageZIndex + Math.Clamp(zIndex, 0, DraggedZIndex - DraggedPageZIndex - 1) : zIndex;
        if (appliedZIndex == valueToApply)
        {
            return;
        }

        appliedZIndex = valueToApply;
        Canvas.SetZIndex(Host, valueToApply);
        Canvas.SetZIndex(backgroundHost, valueToApply);
        Canvas.SetZIndex(focusHost, valueToApply);
    }


    private void ApplyInteractionState() => Host.IsHitTestVisible = interactionEnabled && isFilterMatch && !isGroupStacked;

    private void ApplyIndicatorVisibility()
    {
        bool groupDragging = isGroupDragLeader || isGroupStacked || isLiveDragging;
        focusVisual.Visibility = isKeyboardFocused && !isSelected && !isDragging && !groupDragging ? Visibility.Visible : Visibility.Collapsed;
        selectionVisual.Visibility = isSelected && !isDragging && !groupDragging ? Visibility.Visible : Visibility.Collapsed;
    }


    private void SetGroupTransitions(TimeSpan? duration)
    {
        captureVisibility.HoldForTransition(duration);
        SetTranslationTransition(duration);
        if (scaleTransitionDuration == duration)
        {
            return;
        }

        scaleTransitionDuration = duration;
        Host.ScaleTransition = duration.HasValue ? new Vector3Transition
        {
            Duration = duration.Value
        }

        : null;
        backgroundHost.ScaleTransition = duration.HasValue ? new Vector3Transition
        {
            Duration = duration.Value
        }

        : null;
        focusHost.ScaleTransition = duration.HasValue ? new Vector3Transition
        {
            Duration = duration.Value
        }

        : null;
    }


    private void SetTranslationTransition(TimeSpan? duration)
    {
        if (translationTransitionDuration == duration)
        {
            return;
        }

        translationTransitionDuration = duration;
        Host.TranslationTransition = duration.HasValue ? new Vector3Transition
        {
            Duration = duration.Value
        }

        : null;
        backgroundHost.TranslationTransition = duration.HasValue ? new Vector3Transition
        {
            Duration = duration.Value
        }

        : null;
        focusHost.TranslationTransition = duration.HasValue ? new Vector3Transition
        {
            Duration = duration.Value
        }

        : null;
    }
}
