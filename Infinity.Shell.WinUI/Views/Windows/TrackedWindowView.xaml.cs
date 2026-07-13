using Elysium.UI.WinUI;
using Infinity.Platform.Abstractions;
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
    private const int SelectedZIndex = 1_000_000;
    private const int DraggedZIndex = SelectedZIndex + 1;
    private const int FilteredTierOffset = -100_000;
    private const int UntrackedOrderRank = -50_000;
    private const double ThumbnailDragThreshold = 4.0;
    private static readonly UISettings uiSettings = new();

    private TrackedWindowViewModel? viewModel;
    private TrackedWindowViewModel? subscribedViewModel;
    private IWindowPreview? subscribedPreview;
    private DispatcherQueueTimer? peekTimer;
    private TrackedWindowViewModel? pendingPeekViewModel;
    private TrackedWindowViewModel? peekingViewModel;
    private bool isLoaded;
    private bool isPreviewTargetQueued;
    private int previewUpdateGeneration;
    private double lastPreviewWidth;
    private double lastPreviewHeight;
    private uint? dragPointerId;
    private Point dragStartPoint;
    private UIElement? dragCoordinateRoot;
    private FrameworkElement? dragScrollBoundary;
    private TrackedWindowViewModel? draggedViewModel;
    private double dragScale;
    private double dragStartCanvasLeft;
    private double dragStartCanvasTop;
    private double dragHorizontalDelta;
    private double dragVerticalDelta;
    private bool isThumbnailDragging;
    private bool isPointerOverWindow;

    private readonly IStringLocalizer localizer;

    public TrackedWindowView(IStringLocalizer localizer)
    {
        this.localizer = localizer;
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

        SubscribeToPreview(viewModel?.Preview);

        if (viewModel is not null)
        {
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
        lastPreviewWidth = 0.0;
        lastPreviewHeight = 0.0;

        viewModel = null;

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
            subscribedViewModel = null;
        }

        UnsubscribeFromPreview();
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

        viewModel = args.NewValue as TrackedWindowViewModel;

        SubscribeToPreview(viewModel?.Preview);

        lastPreviewWidth = 0.0;
        lastPreviewHeight = 0.0;

        if (viewModel is not null)
        {
            subscribedViewModel = viewModel;
            subscribedViewModel.PropertyChanged += HandleViewModelPropertyChanged;

            if (isLoaded)
            {
                ApplyFilterState();
                ApplyZIndex();
                QueuePreviewTargetUpdate();
            }
        }
    }

    private void SubscribeToPreview(IWindowPreview? preview)
    {
        if (ReferenceEquals(subscribedPreview, preview))
        {
            return;
        }

        UnsubscribeFromPreview();

        if (preview is null)
        {
            return;
        }

        subscribedPreview = preview;
        subscribedPreview.PreviewInvalidated += HandlePreviewInvalidated;
    }

    private void UnsubscribeFromPreview()
    {
        if (subscribedPreview is null)
        {
            return;
        }

        subscribedPreview.PreviewInvalidated -= HandlePreviewInvalidated;
        subscribedPreview = null;
    }

    private void HandlePreviewInvalidated()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!isLoaded)
            {
                return;
            }

            UpdatePreviewTarget();
        });
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
        dragScrollBoundary = FindDragScrollBoundary();
        dragStartPoint = args.GetCurrentPoint(coordinateRoot).Position;
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
        double horizontalDelta = currentPoint.X - dragStartPoint.X;
        double verticalDelta = currentPoint.Y - dragStartPoint.Y;

        if (!isThumbnailDragging)
        {
            double distance = Math.Sqrt(horizontalDelta * horizontalDelta + verticalDelta * verticalDelta);

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
            dragStartCanvasLeft = Canvas.GetLeft(WindowContainer);
            dragStartCanvasTop = Canvas.GetTop(WindowContainer);

            if (!double.IsFinite(dragStartCanvasLeft))
            {
                dragStartCanvasLeft = currentViewModel.X;
            }

            if (!double.IsFinite(dragStartCanvasTop))
            {
                dragStartCanvasTop = currentViewModel.Y;
            }

            isThumbnailDragging = true;
            SetCanvasZIndex(DraggedZIndex);
            CancelPendingPeek();
            EndPeek();
            ResetHoverScale();
        }

        double boundaryWidth = dragScrollBoundary?.ActualWidth ?? 0;
        double horizontalPosition = double.NaN;

        if (boundaryWidth > 0)
        {
            double boundaryX = args.GetCurrentPoint(dragScrollBoundary).Position.X;
            double clampedBoundaryX = Math.Clamp(boundaryX, 0, boundaryWidth);
            horizontalDelta += clampedBoundaryX - boundaryX;
            horizontalPosition = clampedBoundaryX / boundaryWidth;
        }

        WindowContainer.Translation = new Vector3((float)horizontalDelta, (float)verticalDelta, 0);

        if (draggedViewModel?.MoveThumbnail(horizontalDelta / dragScale,
            verticalDelta / dragScale,
            horizontalPosition) == true)
        {
            dragHorizontalDelta = horizontalDelta;
            dragVerticalDelta = verticalDelta;
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
            SetCloseButtonVisible(true);
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

        if (activeViewModel is not null && commitVisualPosition)
        {
            Canvas.SetLeft(WindowContainer, dragStartCanvasLeft + dragHorizontalDelta);
            Canvas.SetTop(WindowContainer, dragStartCanvasTop + dragVerticalDelta);
        }

        draggedViewModel = null;
        dragPointerId = null;
        dragCoordinateRoot = null;
        dragScrollBoundary = null;
        dragScale = 0;
        dragStartCanvasLeft = 0;
        dragStartCanvasTop = 0;
        dragHorizontalDelta = 0;
        dragVerticalDelta = 0;
        isThumbnailDragging = false;
        WindowContainer.Translation = Vector3.Zero;
        ApplyZIndex();
        activeViewModel?.EndThumbnailDrag();
    }

    private FrameworkElement? FindDragScrollBoundary()
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is TrackedWindowCollectionView collectionView)
            {
                return collectionView.DragScrollBoundary;
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
        IWindowPreview? preview = viewModel?.Preview;

        if (preview is null || ThumbnailHost.ActualWidth <= 0.0 || ThumbnailHost.ActualHeight <= 0.0)
        {
            return;
        }

        double width = ThumbnailHost.ActualWidth;
        double height = ThumbnailHost.ActualHeight;

        if (Math.Abs(width - lastPreviewWidth) < 0.5 && Math.Abs(height - lastPreviewHeight) < 0.5)
        {
            return;
        }

        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (!ThumbnailProxyManager.TryAttach(preview, ThumbnailHost, out nint proxyHandle))
            {
                return;
            }

            if (!ThumbnailProxyManager.UpdateSize(preview, width, height))
            {
                continue;
            }

            lastPreviewWidth = width;
            lastPreviewHeight = height;
            viewModel!.SetPreviewTarget(proxyHandle, width, height);
            return;
        }
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
            SetCanvasZIndex(isThumbnailDragging ? DraggedZIndex : ComputeZIndex());
        }
        catch
        {
        }
    }

    private int ComputeZIndex()
    {
        if (viewModel is null)
        {
            return 0;
        }

        if (viewModel.IsSelected)
        {
            return SelectedZIndex;
        }

        int zIndex = viewModel.ZIndex ?? int.MaxValue;
        int orderRank = zIndex == int.MaxValue ? UntrackedOrderRank : -zIndex;

        return viewModel.IsFiltered ? orderRank + FilteredTierOffset : orderRank;
    }

    private void SetCanvasZIndex(int zIndex)
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(current);

            if (parent is Canvas)
            {
                Canvas.SetZIndex((UIElement)current, zIndex);
                return;
            }

            current = parent;
        }
    }
}
