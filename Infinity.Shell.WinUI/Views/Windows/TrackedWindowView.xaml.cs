using Elysium.UI.WinUI;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace Infinity.Shell.WinUI;

public partial class TrackedWindowView :
    UserControl
{
    private const int DraggedZIndex = 1_000_000;
    private const int SelectedZIndex = DraggedZIndex - 1;
    private const int FilteredTierOffset = -100_000;
    private const int UntrackedOrderRank = -50_000;
    private const double ThumbnailDragThreshold = 4.0;
    private static readonly UISettings uiSettings = new();

    private TrackedWindowViewModel? viewModel;
    private TrackedWindowViewModel? subscribedViewModel;
    private ThumbnailCompositionPreview? preview;
    private DispatcherQueueTimer? peekTimer;
    private TrackedWindowViewModel? pendingPeekViewModel;
    private TrackedWindowViewModel? peekingViewModel;
    private bool isLoaded;
    private bool isPreviewTargetQueued;
    private int previewUpdateGeneration;
    private uint? dragPointerId;
    private Point dragStartPoint;
    private UIElement? dragCoordinateRoot;
    private FrameworkElement? dragScrollBoundary;
    private TrackedWindowViewModel? draggedViewModel;
    private double dragScale;
    private double dragHorizontalDelta;
    private double dragVerticalDelta;
    private bool ownsDragScrollSession;
    private bool isDragVisualPendingReset;
    private bool isDragZIndexElevated;
    private bool isThumbnailDragging;
    private bool isPointerOverWindow;

    private readonly IStringLocalizer localizer;
    private readonly IThumbnailDragScroller thumbnailDragScroller;
    private readonly IWindowNavigationCoordinator windowNavigationCoordinator;
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly ILogger<TrackedWindowView> logger;

    public TrackedWindowView(IStringLocalizer localizer,
        IThumbnailDragScroller thumbnailDragScroller,
        IWindowNavigationCoordinator windowNavigationCoordinator,
        IWindowPreviewSurface windowPreviewSurface,
        ILogger<TrackedWindowView> logger)
    {
        this.localizer = localizer;
        this.thumbnailDragScroller = thumbnailDragScroller;
        this.windowNavigationCoordinator = windowNavigationCoordinator;
        this.windowPreviewSurface = windowPreviewSurface;
        this.logger = logger;
        InitializeComponent();

        DataContextChanged += HandleDataContextChanged;
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        SizeChanged += HandleSizeChanged;

        ThumbnailGrid.SizeChanged += HandleThumbnailGridSizeChanged;
        ThumbnailShadowHost.SizeChanged += HandleThumbnailShadowHostSizeChanged;
        ThumbnailHost.SizeChanged += HandleThumbnailHostSizeChanged;
    }

    public TrackedWindowViewModel ViewModel => (TrackedWindowViewModel)DataContext;

    public Visibility ToSelectionVisibility(bool isSelected) =>
        isSelected ? Visibility.Visible : Visibility.Collapsed;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        isLoaded = true;
        viewModel = DataContext as TrackedWindowViewModel;

        try
        {
            ElementCompositionPreview.SetIsTranslationEnabled(WindowContainer, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ThumbnailGrid, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ThumbnailShadowHost, true);

            Visual? closeButtonVisual = GetCloseButtonVisual();

            if (closeButtonVisual is not null)
            {
                closeButtonVisual.Opacity = 0.0f;
            }
        }
        catch
        {
        }

        if (viewModel is not null)
        {
            preview = ThumbnailCompositionPreview.Create(windowPreviewSurface,
                viewModel.Handle,
                ThumbnailHost,
                logger);
            ApplyFilterState();
            ApplyZIndex();
            QueuePreviewTargetUpdate();
        }
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        CompleteThumbnailDrag(false);
        WindowContainer.ReleasePointerCaptures();
        CancelPendingPeek();
        EndPeek();
        DisposePeekTimer();

        isLoaded = false;
        isPointerOverWindow = false;
        previewUpdateGeneration++;
        isPreviewTargetQueued = false;
        viewModel = null;

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
            subscribedViewModel = null;
        }

        preview?.Dispose();
        preview = null;
    }

    private void HandleDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        CompleteThumbnailDrag(false);
        WindowContainer.ReleasePointerCaptures();
        CancelPendingPeek();
        EndPeek();

        previewUpdateGeneration++;
        isPreviewTargetQueued = false;

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
            subscribedViewModel = null;
        }

        preview?.Dispose();
        preview = null;
        viewModel = args.NewValue as TrackedWindowViewModel;

        if (viewModel is not null)
        {
            subscribedViewModel = viewModel;
            subscribedViewModel.PropertyChanged += HandleViewModelPropertyChanged;

            if (isLoaded)
            {
                preview = ThumbnailCompositionPreview.Create(windowPreviewSurface,
                    viewModel.Handle,
                    ThumbnailHost,
                    logger);
                ApplyFilterState();
                ApplyZIndex();
                QueuePreviewTargetUpdate();
            }
        }
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) =>
        QueuePreviewTargetUpdate();

    private void HandleThumbnailGridSizeChanged(object sender, SizeChangedEventArgs args) =>
        QueuePreviewTargetUpdate();

    private void HandleThumbnailShadowHostSizeChanged(object sender, SizeChangedEventArgs args) =>
        QueuePreviewTargetUpdate();

    private void HandleThumbnailHostSizeChanged(object sender, SizeChangedEventArgs args) =>
        QueuePreviewTargetUpdate();

    private void HandleWindowContainerPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        isPointerOverWindow = true;

        if (isThumbnailDragging)
        {
            return;
        }

        SetCloseButtonVisible(true);
        AnimateHoverScale(true);
        QueuePeek();
    }

    private void HandleWindowContainerPointerExited(object sender, PointerRoutedEventArgs args)
    {
        isPointerOverWindow = false;
        SetCloseButtonVisible(false);
        AnimateHoverScale(false);
        CancelPendingPeek();
        EndPeek();
    }

    private void HandleWindowContainerPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (IsButtonSource(args.OriginalSource))
        {
            return;
        }

        var pointerPoint = args.GetCurrentPoint(WindowContainer);

        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        UIElement coordinateRoot = XamlRoot?.Content as UIElement ?? this;

        if (!WindowContainer.CapturePointer(args.Pointer))
        {
            return;
        }

        dragPointerId = args.Pointer.PointerId;
        dragCoordinateRoot = coordinateRoot;
        dragStartPoint = args.GetCurrentPoint(coordinateRoot).Position;
        windowNavigationCoordinator.Activate(ViewModel.Handle);
        CancelPendingPeek();
        EndPeek();
        ResetHoverScale();
        isDragZIndexElevated = true;
        ApplyZIndex();

        args.Handled = true;
    }

    private void HandleWindowContainerPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId || dragCoordinateRoot is null)
        {
            return;
        }

        args.Handled = true;

        Point currentPoint = args.GetCurrentPoint(dragCoordinateRoot).Position;
        double horizontalDistance = currentPoint.X - dragStartPoint.X;
        double verticalDistance = currentPoint.Y - dragStartPoint.Y;

        if (!isThumbnailDragging)
        {
            double distance = Math.Sqrt(horizontalDistance * horizontalDistance + verticalDistance * verticalDistance);

            if (distance < ThumbnailDragThreshold)
            {
                return;
            }

            TrackedWindowViewModel currentViewModel = ViewModel;
            double currentScale = currentViewModel.LayoutScale;

            if (!double.IsFinite(currentScale) || currentScale <= 0 || !currentViewModel.BeginThumbnailDrag())
            {
                CompleteThumbnailDrag(false);
                WindowContainer.ReleasePointerCapture(args.Pointer);
                return;
            }

            draggedViewModel = currentViewModel;
            dragScale = currentScale;
            dragScrollBoundary = FindThumbnailDragScrollBoundary();

            isThumbnailDragging = true;
            SetCloseButtonVisible(false);
            ownsDragScrollSession = thumbnailDragScroller.Begin(currentViewModel.Handle);
            CancelPendingPeek();
            EndPeek();
            ResetHoverScale();
        }

        WindowContainer.Translation = new Vector3((float)horizontalDistance, (float)verticalDistance, 0);

        if (draggedViewModel?.MoveThumbnail(horizontalDistance / dragScale,
            verticalDistance / dragScale) == true)
        {
            dragHorizontalDelta = horizontalDistance;
            dragVerticalDelta = verticalDistance;
            UpdateThumbnailDragScroll(args);
        }
        else
        {
            CompleteThumbnailDrag(false);
            WindowContainer.ReleasePointerCapture(args.Pointer);
        }
    }

    private void HandleWindowContainerPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        bool wasDragging = isThumbnailDragging;
        CompleteThumbnailDrag();
        WindowContainer.ReleasePointerCapture(args.Pointer);
        args.Handled = true;

        if (!wasDragging)
        {
            ViewModel.Navigate();
            return;
        }

        if (isPointerOverWindow)
        {
            QueuePeek();
        }
    }

    private void HandleWindowContainerPointerCanceled(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        CompleteThumbnailDrag();
        WindowContainer.ReleasePointerCapture(args.Pointer);
        args.Handled = true;
    }

    private void HandleWindowContainerPointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId == args.Pointer.PointerId)
        {
            CompleteThumbnailDrag();
        }
    }

    private void CompleteThumbnailDrag(bool commitVisualPosition = true)
    {
        TrackedWindowViewModel? activeViewModel = draggedViewModel;

        if (activeViewModel is not null && ownsDragScrollSession)
        {
            thumbnailDragScroller.End(activeViewModel.Handle);
        }

        draggedViewModel = null;
        dragPointerId = null;
        dragCoordinateRoot = null;
        dragScrollBoundary = null;
        dragScale = 0;
        bool hasVisualDelta = dragHorizontalDelta != 0 || dragVerticalDelta != 0;
        dragHorizontalDelta = 0;
        dragVerticalDelta = 0;
        ownsDragScrollSession = false;
        isThumbnailDragging = false;
        SetCloseButtonVisible(isPointerOverWindow);
        isDragVisualPendingReset = activeViewModel is not null && commitVisualPosition && hasVisualDelta;
        isDragZIndexElevated = false;
        ApplyZIndex();

        activeViewModel?.EndThumbnailDrag();

        if (!isDragVisualPendingReset)
        {
            ResetDragVisual();
            QueuePreviewTargetUpdate();
        }
    }

    private void ResetDragVisual()
    {
        isDragVisualPendingReset = false;
        WindowContainer.Translation = Vector3.Zero;
    }

    private void UpdateThumbnailDragScroll(PointerRoutedEventArgs args)
    {
        if (!ownsDragScrollSession || draggedViewModel is null || dragScrollBoundary is null)
        {
            return;
        }

        double viewportWidth = dragScrollBoundary.ActualWidth;
        double pointerX = args.GetCurrentPoint(dragScrollBoundary).Position.X;
        thumbnailDragScroller.Update(draggedViewModel.Handle, pointerX, viewportWidth);
    }

    private FrameworkElement? FindThumbnailDragScrollBoundary()
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is TrackedWindowCollectionView collectionView)
            {
                return collectionView.ThumbnailDragScrollBoundary;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsButtonSource(object source)
    {
        DependencyObject? current = source as DependencyObject;

        while (current is not null)
        {
            if (current is Button)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void HandleWindowContainerRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        args.Handled = true;
        CancelPendingPeek();
        EndPeek();

        TrackedWindowViewModel currentViewModel = ViewModel;
        int? currentPage = currentViewModel.GetCurrentPage();
        int? openingPage = currentViewModel.GetOpeningPage();
        IReadOnlyList<WindowPageTarget> targets = currentViewModel.GetPageTargets(openingPage);
        MenuFlyout menu = new();
        MenuFlyoutItem stickyItem = new()
        {
            Text = localizer.GetString(currentViewModel.IsSticky
                ? "UnpinWindowMenuItem"
                : "PinWindowMenuItem"),
            Icon = new FontIcon
            {
                Glyph = currentViewModel.IsSticky ? "\uE77A" : "\uE718"
            }
        };
        stickyItem.Click += (_, _) => currentViewModel.ToggleSticky();
        menu.Items.Add(stickyItem);

        if (!currentViewModel.IsSticky)
        {
            menu.Items.Add(new MenuFlyoutSeparator());

            MenuFlyoutSubItem moveSubMenu = new()
            {
                Text = localizer.GetString("MoveWindowToPageMenuItem")
            };

            foreach (WindowPageTarget target in targets)
            {
                ToggleMenuFlyoutItem item = new()
                {
                    Text = target.DisplayName,
                    IsChecked = target.Page == currentPage
                };
                item.Click += (_, _) => currentViewModel.MoveToPage(target.Page);
                moveSubMenu.Items.Add(item);
            }

            menu.Items.Add(moveSubMenu);
        }

        if (currentViewModel.CanCreatePlacementRule)
        {
            if (currentViewModel.IsSticky)
            {
                menu.Items.Add(new MenuFlyoutSeparator());
            }

            MenuFlyoutSubItem ruleSubMenu = new()
            {
                Text = localizer.GetString("AlwaysOpenApplicationOnPageMenuItem")
            };

            foreach (WindowPageTarget target in targets)
            {
                ToggleMenuFlyoutItem item = new()
                {
                    Text = target.DisplayName,
                    IsChecked = target.Page == openingPage
                };
                item.Click += async (_, _) => await currentViewModel.SetOpeningPageAsync(target.Page);
                ruleSubMenu.Items.Add(item);
            }

            menu.Items.Add(ruleSubMenu);

            if (openingPage.HasValue)
            {
                MenuFlyoutItem removeRuleItem = new()
                {
                    Text = localizer.GetString("RemoveApplicationPageRuleMenuItem")
                };
                removeRuleItem.Click += async (_, _) => await currentViewModel.RemoveOpeningPageRuleAsync();
                menu.Items.Add(new MenuFlyoutSeparator());
                menu.Items.Add(removeRuleItem);
            }
        }

        menu.ShowAt(WindowContainer, args.GetPosition(WindowContainer));
    }

    private void QueuePeek()
    {
        if (!isLoaded ||
            isThumbnailDragging ||
            viewModel is null ||
            ReferenceEquals(pendingPeekViewModel, viewModel) ||
            ReferenceEquals(peekingViewModel, viewModel))
        {
            return;
        }

        CancelPendingPeek();

        peekTimer ??= CreatePeekTimer();
        pendingPeekViewModel = viewModel;
        peekTimer.Interval = TimeSpan.FromMilliseconds(uiSettings.MouseHoverTime);
        peekTimer.Start();
    }

    private DispatcherQueueTimer CreatePeekTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.IsRepeating = false;
        timer.Tick += HandlePeekTimerTick;
        return timer;
    }

    private void HandlePeekTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();

        TrackedWindowViewModel? pendingViewModel = pendingPeekViewModel;
        pendingPeekViewModel = null;

        if (!isLoaded || pendingViewModel is null || !ReferenceEquals(pendingViewModel, viewModel))
        {
            return;
        }

        EndPeek();
        peekingViewModel = pendingViewModel;
        peekingViewModel.BeginPeek();
    }

    private void CancelPendingPeek()
    {
        peekTimer?.Stop();
        pendingPeekViewModel = null;
    }

    private void EndPeek()
    {
        TrackedWindowViewModel? activeViewModel = peekingViewModel;
        peekingViewModel = null;
        activeViewModel?.EndPeek();
    }

    private void DisposePeekTimer()
    {
        if (peekTimer is null)
        {
            return;
        }

        peekTimer.Stop();
        peekTimer.Tick -= HandlePeekTimerTick;
        peekTimer = null;
    }

    private void HandleCloseButtonPointerEntered(object sender, PointerRoutedEventArgs args) =>
        SetCloseButtonVisible(true);

    private void HandleCloseButtonPointerExited(object sender, PointerRoutedEventArgs args) =>
        SetCloseButtonVisible(false);

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!isLoaded)
        {
            return;
        }

        if (!DispatcherQueue.HasThreadAccess)
        {
            TrackedWindowViewModel? currentViewModel = subscribedViewModel;
            string? propertyName = args.PropertyName;

            DispatcherQueue.TryEnqueue(() =>
            {
                if (!isLoaded || currentViewModel != subscribedViewModel)
                {
                    return;
                }

                ApplyFromPropertyName(propertyName);
            });

            return;
        }

        ApplyFromPropertyName(args.PropertyName);
    }

    private void ApplyFromPropertyName(string? propertyName)
    {
        if (isDragVisualPendingReset &&
            (propertyName == nameof(TrackedWindowViewModel.X) || propertyName == nameof(TrackedWindowViewModel.Y)))
        {
            ResetDragVisual();
        }

        if (propertyName == nameof(TrackedWindowViewModel.IsFiltered))
        {
            ApplyFilterState();
            ApplyZIndex();
        }
        else if (propertyName == nameof(TrackedWindowViewModel.IsSelected) || propertyName == nameof(TrackedWindowViewModel.ZIndex))
        {
            ApplyZIndex();
        }

        if (propertyName == nameof(TrackedWindowViewModel.Width) ||
            propertyName == nameof(TrackedWindowViewModel.Height) ||
            propertyName == nameof(TrackedWindowViewModel.IsFiltered) ||
            propertyName == nameof(TrackedWindowViewModel.IsVisible))
        {
            QueuePreviewTargetUpdate();
        }
    }

    private void QueuePreviewTargetUpdate()
    {
        if (!isLoaded || isPreviewTargetQueued)
        {
            return;
        }

        isPreviewTargetQueued = true;
        int generation = previewUpdateGeneration;

        bool enqueued = DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != previewUpdateGeneration)
            {
                return;
            }

            isPreviewTargetQueued = false;

            if (!isLoaded)
            {
                return;
            }

            UpdatePreviewTarget();
        });

        if (!enqueued && generation == previewUpdateGeneration)
        {
            isPreviewTargetQueued = false;
        }
    }

    private void UpdatePreviewTarget()
    {
        if (preview is null || viewModel is null)
        {
            return;
        }

        double width = ThumbnailHost.ActualWidth;
        double height = ThumbnailHost.ActualHeight;
        bool isVisible = width > 0.0 &&
            height > 0.0 &&
            viewModel.IsVisible &&
            !viewModel.IsFiltered &&
            Visibility == Visibility.Visible;
        preview.Update(width, height, isVisible);
    }

    private Visual? GetContainerVisual()
    {
        if (!isLoaded || WindowContainer is null)
        {
            return null;
        }

        try
        {
            return ElementCompositionPreview.GetElementVisual(WindowContainer);
        }
        catch
        {
            return null;
        }
    }

    private Visual? GetCloseButtonVisual()
    {
        if (!isLoaded || CloseButton is null)
        {
            return null;
        }

        try
        {
            return ElementCompositionPreview.GetElementVisual(CloseButton);
        }
        catch
        {
            return null;
        }
    }

    private void AnimateHoverScale(bool entered)
    {
        Visual? visual = GetContainerVisual();

        if (visual is null)
        {
            return;
        }

        try
        {
            visual.CenterPoint = new Vector3(
                (float)(WindowContainer.ActualWidth / 2),
                (float)(WindowContainer.ActualHeight / 2),
                0);

            visual.StopAnimation("Scale");

            Compositor compositor = visual.Compositor;
            Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            float scale = entered ? 1.03f : 1.0f;
            scaleAnimation.InsertKeyFrame(1.0f, new Vector3(scale, scale, 1.0f));
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(150);

            visual.StartAnimation("Scale", scaleAnimation);
        }
        catch
        {
        }
    }

    private void ResetHoverScale()
    {
        Visual? visual = GetContainerVisual();

        if (visual is null)
        {
            return;
        }

        try
        {
            visual.StopAnimation("Scale");
            visual.Scale = Vector3.One;
        }
        catch
        {
        }
    }

    private void SetCloseButtonVisible(bool visible)
    {
        if (!isLoaded)
        {
            return;
        }

        if (visible && viewModel?.IsFiltered == true)
        {
            return;
        }

        Visual? visual = GetCloseButtonVisual();

        if (visual is null)
        {
            return;
        }

        try
        {
            visual.StopAnimation("Opacity");
            visual.Opacity = visible ? 1.0f : 0.0f;
        }
        catch
        {
        }
    }

    private void ApplyFilterState()
    {
        if (!isLoaded || viewModel is null)
        {
            return;
        }

        try
        {
            if (viewModel.IsFiltered)
            {
                SetCloseButtonVisible(false);
                Opacity = 0.0;
                Visibility = Visibility.Collapsed;
            }
            else
            {
                Visibility = Visibility.Visible;
                Opacity = 1.0;
            }
        }
        catch
        {
        }
    }

    private void ApplyZIndex()
    {
        if (!isLoaded || viewModel is null)
        {
            return;
        }

        try
        {
            SetCanvasZIndex(isDragZIndexElevated ? DraggedZIndex : ComputeZIndex());
        }
        catch
        {
        }
    }

    internal static int ComputeZIndexRank(TrackedWindowViewModel viewModel)
    {
        if (viewModel.IsSelected)
        {
            return SelectedZIndex;
        }

        int zIndex = viewModel.ZIndex ?? int.MaxValue;
        int orderRank = zIndex == int.MaxValue ? UntrackedOrderRank : -zIndex;

        return viewModel.IsFiltered ? orderRank + FilteredTierOffset : orderRank;
    }

    private int ComputeZIndex() =>
        viewModel is null ? 0 : ComputeZIndexRank(viewModel);

    private void SetCanvasZIndex(int zIndex)
    {
        UIElement? container = FindWindowItemContainer();

        if (container is not null)
        {
            Canvas.SetZIndex(container, zIndex);
        }
    }

    private UIElement? FindWindowItemContainer()
    {
        TrackedWindowCollectionView? collectionView = FindWindowCollectionView();

        return collectionView?.GetWindowItemContainer(ViewModel);
    }

    private TrackedWindowCollectionView? FindWindowCollectionView()
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is TrackedWindowCollectionView collectionView)
            {
                return collectionView;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
