using Elysium.Platform.Abstractions;
using Elysium.UI.Controls.WinUI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public partial class PageTintView :
    DesktopOverlay
{
    private readonly IMonitorLocator monitorLocator;
    private readonly ITaskbarLocator taskbarLocator;
    private PageTintViewModel? subscribedViewModel;
    private DropShadow? dropShadow;
    private SpriteVisual? shadowVisual;

    public PageTintView(IMonitorLocator monitorLocator, ITaskbarLocator taskbarLocator)
    {
        InitializeComponent();

        this.monitorLocator = monitorLocator;
        this.taskbarLocator = taskbarLocator;

        ShadowContainer.SizeChanged += HandleShadowContainerSizeChanged;
        DataContextChanged += HandleDataContextChanged;
        Loaded += HandleLoaded;
    }

    public PageTintViewModel ViewModel => (PageTintViewModel)DataContext;

    private Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private Visibility ToInverseVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private DesktopOverlayHeaderPlacement ToHeaderPlacement(PreviewPosition position)
    {
        if (position == PreviewPosition.Auto)
        {
            return ResolveAutoHeaderPlacement();
        }

        return position switch
        {
            PreviewPosition.Top => DesktopOverlayHeaderPlacement.Bottom,
            PreviewPosition.Bottom => DesktopOverlayHeaderPlacement.Top,
            _ => DesktopOverlayHeaderPlacement.Bottom
        };
    }

    private DesktopOverlayHeaderPlacement ResolveAutoHeaderPlacement()
    {
        WindowHandle window = new(Handle);

        if (window.IsNull)
        {
            return DesktopOverlayHeaderPlacement.Bottom;
        }

        MonitorHandle monitor = monitorLocator.GetMonitorForWindow(window);
        TaskbarInfo? taskbar = taskbarLocator.GetTaskbarForMonitor(monitor);

        if (taskbar is null)
        {
            return DesktopOverlayHeaderPlacement.Bottom;
        }

        return taskbar.Value.Edge switch
        {
            TaskbarEdge.Top => DesktopOverlayHeaderPlacement.Bottom,
            TaskbarEdge.Bottom => DesktopOverlayHeaderPlacement.Top,
            _ => DesktopOverlayHeaderPlacement.Bottom
        };
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        EnsureSubscribed();
        UpdateBindings();
    }

    protected override void OnOpened() => UpdateBindings();

    private void HandleDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        EnsureSubscribed();
        UpdateBindings();
    }

    private void EnsureSubscribed()
    {
        PageTintViewModel? current = DataContext as PageTintViewModel;

        if (subscribedViewModel == current)
        {
            return;
        }

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        }

        subscribedViewModel = current;

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(PageTintViewModel.IsEditing) && ViewModel.IsEditing)
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                TitleTextBox.Focus(FocusState.Programmatic);
                TitleTextBox.SelectAll();
            });
        }

        if (args.PropertyName == nameof(PageTintViewModel.PreviewPosition))
        {
            UpdateBindings();
        }
    }

    private void UpdateBindings()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateBindings);
            return;
        }

        Bindings.Update();
    }

    private void HandleShadowContainerSizeChanged(object sender, SizeChangedEventArgs args)
    {
        ShadowContainer.DispatcherQueue.TryEnqueue(() =>
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(ShadowCanvas);
            Compositor compositor = visual.Compositor;

            if (shadowVisual is null)
            {
                dropShadow = compositor.CreateDropShadow();
                dropShadow.Color = Windows.UI.Color.FromArgb(120, 0, 0, 0);
                dropShadow.BlurRadius = 24f;
                dropShadow.Offset = new Vector3(0, 8, 0);

                shadowVisual = compositor.CreateSpriteVisual();
                shadowVisual.Shadow = dropShadow;

                ElementCompositionPreview.SetElementChildVisual(ShadowCanvas, shadowVisual);
            }

            if (ShadowContainer.ActualWidth <= 0 || ShadowContainer.ActualHeight <= 0)
            {
                return;
            }

            const float margin = 24f;
            shadowVisual.Offset = new Vector3(margin, margin, 0);
            shadowVisual.Size = new Vector2(
                (float)ShadowContainer.ActualWidth - (margin * 2),
                (float)ShadowContainer.ActualHeight - (margin * 2));
        });
    }
}