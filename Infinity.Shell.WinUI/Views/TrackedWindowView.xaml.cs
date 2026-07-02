using Infinity.Platform.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed partial class TrackedWindowView :
    UserControl
{
    private const int SelectedZIndex = 1_000_000;
    private const int FilteredTierOffset = -100_000;
    private const int UntrackedOrderRank = -50_000;

    private TrackedWindowViewModel? viewModel;
    private TrackedWindowViewModel? subscribedViewModel;
    private IWindowPreview? subscribedPreview;
    private bool isLoaded;
    private bool isPreviewTargetQueued;
    private Storyboard? filterStateStoryboard;

    public TrackedWindowView()
    {
        InitializeComponent();

        DataContextChanged += HandleDataContextChanged;
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        SizeChanged += HandleSizeChanged;

        ThumbnailGrid.SizeChanged += HandleThumbnailGridSizeChanged;
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
            ElementCompositionPreview.SetIsTranslationEnabled(ThumbnailGrid, true);

            Visual? thumbnailVisual = GetThumbnailVisual();
            thumbnailVisual?.CenterPoint = new Vector3((float)(ThumbnailGrid.ActualWidth / 2), (float)(ThumbnailGrid.ActualHeight / 2), 0);

            Visual? closeButtonVisual = GetCloseButtonVisual();
            closeButtonVisual?.Opacity = 0.0f;
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
        isLoaded = false;
        isPreviewTargetQueued = false;

        viewModel = null;

        subscribedViewModel?.PropertyChanged -= HandleViewModelPropertyChanged;
        subscribedViewModel = null;

        UnsubscribeFromPreview();
    }

    private void HandleDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        subscribedViewModel?.PropertyChanged -= HandleViewModelPropertyChanged;
        subscribedViewModel = null;

        viewModel = args.NewValue as TrackedWindowViewModel;

        SubscribeToPreview(viewModel?.Preview);

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

    private void HandleThumbnailGridSizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateThumbnailCenterPoint();
        QueuePreviewTargetUpdate();
    }

    private void HandleThumbnailHostSizeChanged(object sender, SizeChangedEventArgs args) =>
        QueuePreviewTargetUpdate();

    private void HandleWindowContainerPointerEntered(object sender, PointerRoutedEventArgs args) =>
        SetCloseButtonVisible(true);

    private void HandleWindowContainerPointerExited(object sender, PointerRoutedEventArgs args) =>
        SetCloseButtonVisible(false);

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

        DispatcherQueue.TryEnqueue(() =>
        {
            isPreviewTargetQueued = false;

            if (!isLoaded)
            {
                return;
            }

            UpdatePreviewTarget();
        });
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

            viewModel!.SetPreviewTarget(proxyHandle, width, height);
            return;
        }
    }

    private void UpdateThumbnailCenterPoint()
    {
        Visual? visual = GetThumbnailVisual();

        if (visual is null)
        {
            return;
        }

        try
        {
            visual.CenterPoint = new Vector3((float)(ThumbnailGrid.ActualWidth / 2), (float)(ThumbnailGrid.ActualHeight / 2), 0);
        }
        catch
        {
        }
    }

    private Visual? GetThumbnailVisual()
    {
        if (!isLoaded || ThumbnailGrid is null)
        {
            return null;
        }

        try
        {
            return ElementCompositionPreview.GetElementVisual(ThumbnailGrid);
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

            Compositor compositor = visual.Compositor;
            ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.InsertKeyFrame(1.0f, visible ? 1.0f : 0.0f);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(120);

            visual.StartAnimation("Opacity", opacityAnimation);
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
            }

            filterStateStoryboard?.Stop();

            if (viewModel.IsFiltered)
            {
                DoubleAnimation opacityAnimation = new()
                {
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard.SetTarget(opacityAnimation, this);
                Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

                filterStateStoryboard = new Storyboard();
                filterStateStoryboard.Children.Add(opacityAnimation);
                filterStateStoryboard.Completed += (sender, args) =>
                {
                    if (isLoaded && viewModel?.IsFiltered == true)
                    {
                        Visibility = Visibility.Collapsed;
                    }
                };
                filterStateStoryboard.Begin();
            }
            else
            {
                Opacity = 0.0;
                Visibility = Visibility.Visible;

                DoubleAnimation opacityAnimation = new()
                {
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard.SetTarget(opacityAnimation, this);
                Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

                filterStateStoryboard = new Storyboard();
                filterStateStoryboard.Children.Add(opacityAnimation);
                filterStateStoryboard.Begin();
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
            SetCanvasZIndex(ComputeZIndex());
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