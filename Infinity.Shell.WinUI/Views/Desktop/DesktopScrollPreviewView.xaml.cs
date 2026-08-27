using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.System;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopScrollPreviewView :
    UserControl
{
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly IWindowCollection windowCollection;
    private readonly IShellLayoutCalculator layoutCalculator;
    private readonly IPanState panState;
    private readonly IScroller scroller;
    private readonly IPager pager;
    private readonly IWorkspace workspace;
    private readonly DesktopPageLayoutCalculator pageLayoutCalculator;
    private readonly DesktopSnapPlacementResolver snapPlacementResolver;
    private readonly DesktopSnapSlotOccupancyResolver snapSlotOccupancyResolver;
    private readonly DesktopScrollPreviewAnimator animator;
    private readonly DesktopPageStrip pageStrip;
    private readonly DesktopWindowPreviewCollection previews;
    private readonly DesktopDragCursorConfinement cursorConfinement;
    private readonly DesktopOverviewConfiguration overviewConfiguration;
    private readonly DesktopApplicationLaunchCoordinator applicationLaunchCoordinator;
    private CancellationTokenSource? applicationLaunchCancellation;
    private bool eventsSubscribed;
    private bool filterActive;
    private bool shiftKeyDown;
    private bool isRunning;
    private DesktopPageReorderPreviewState? pageReorderState;
    private int pageBeforeFilter = -1;
    private double spacingProgress = 1;
    private int monitorOriginX;
    private int monitorOriginY;
    private int overlayScreenOriginY;
    private int workAreaOffsetY;
    private nint activeSnapWindow;
    private double activeSnapPointerX;
    private double activeSnapPointerY;

    public DesktopScrollPreviewView(IWindowPreviewSurface windowPreviewSurface, IWindowCollection windowCollection, IShellLayoutCalculator layoutCalculator, IPanState panState, IScroller scroller, IPager pager, IWorkspace workspace, DesktopPageLayoutCalculator pageLayoutCalculator, DesktopSnapPlacementResolver snapPlacementResolver, DesktopSnapSlotOccupancyResolver snapSlotOccupancyResolver, DesktopScrollPreviewAnimator animator, DesktopPageStrip pageStrip, DesktopWindowPreviewCollection previews, DesktopDragCursorConfinement cursorConfinement, DesktopOverviewConfiguration overviewConfiguration, DesktopShortcutHintsViewModel shortcutHints, DesktopApplicationPickerViewModel applicationPicker, DesktopApplicationLaunchCoordinator applicationLaunchCoordinator)
    {
        InitializeComponent();

        this.windowPreviewSurface = windowPreviewSurface;
        this.windowCollection = windowCollection;
        this.layoutCalculator = layoutCalculator;
        this.panState = panState;
        this.scroller = scroller;
        this.pager = pager;
        this.workspace = workspace;
        this.pageLayoutCalculator = pageLayoutCalculator;
        this.snapPlacementResolver = snapPlacementResolver;
        this.snapSlotOccupancyResolver = snapSlotOccupancyResolver;
        this.animator = animator;
        this.pageStrip = pageStrip;
        this.previews = previews;
        this.cursorConfinement = cursorConfinement;
        this.overviewConfiguration = overviewConfiguration;
        this.applicationLaunchCoordinator = applicationLaunchCoordinator;
        ShortcutHints = shortcutHints;
        ApplicationPicker = applicationPicker;

        this.pageStrip.PageInvoked += HandlePageInvoked;
        this.pageStrip.ApplicationPickerRequested += HandleApplicationPickerRequested;
        this.pageStrip.ReorderPreviewChanged += HandlePageReorderPreviewChanged;
        this.previews.WindowInvoked += HandleWindowInvoked;
        this.previews.WindowPositionChanged += HandleWindowPositionChanged;
        this.previews.WindowDragMoved += HandleWindowDragMoved;
        this.previews.WindowDragCompleted += HandleWindowDragCompleted;

        ElementCompositionPreview.SetIsTranslationEnabled(PreviewSurface, true);
    }

    public event EventHandler? BackgroundInvoked;

    public event EventHandler? InputFocusRequested;

    public event Action<int>? PageInvoked;

    public event EventHandler? SettingsInvoked;

    public event Action<nint>? WindowInvoked;

    public bool IsRunning => isRunning;

    public DesktopShortcutHintsViewModel ShortcutHints { get; }

    public DesktopApplicationPickerViewModel ApplicationPicker { get; }

    public Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToStaticVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToApplicationIconVisibility(ApplicationIcon? icon) => icon is null ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility ToFallbackIconVisibility(ApplicationIcon? icon) => icon is null ? Visibility.Visible : Visibility.Collapsed;

    public static ImageSource? CreateApplicationIconSource(ApplicationIcon? icon)
    {
        if (icon is null || icon.Width <= 0 || icon.Height <= 0 || icon.Pixels.Length != icon.Width * icon.Height * 4)
        {
            return null;
        }

        WriteableBitmap bitmap = new(icon.Width, icon.Height);

        using Stream stream = bitmap.PixelBuffer.AsStream();
        stream.Write(icon.Pixels);
        return bitmap;
    }

#if DEBUG
    internal void OpenApplicationPickerForDebug() => pageStrip.RequestApplicationPickerForDebug();
#endif

    public void Prepare(nint ownerWindowHandle, int screenOriginY)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => Prepare(ownerWindowHandle, screenOriginY));
            return;
        }

        overlayScreenOriginY = screenOriginY;
        bool originChanged = RefreshMonitorOrigin();
        cursorConfinement.SetOwner(ownerWindowHandle);
        windowPreviewSurface.Initialize(ownerWindowHandle);

        if (!isRunning)
        {
            isRunning = true;
            spacingProgress = 1;

            SubscribeEvents();
            pageStrip.Start(PageCanvas, PageShadowCanvas, PageTitleCanvas, PreviewSurface, animator.Scale);
            Synchronise();

            SetInteractionEnabled(false);
            Opacity = 1;
        }
        else if (originChanged)
        {
            Synchronise();
        }
    }

    public void AnimateInward()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(AnimateInward);
            return;
        }

        if (!isRunning)
        {
            return;
        }

        bool restoreSpacing = spacingProgress != 1;
        spacingProgress = 1;
        SetInteractionEnabled(false);
        RefreshLayout(restoreSpacing ? animator.EnterDuration : null);

        animator.AnimateInward(PreviewSurface, GetAnimationWidth(), GetAnimationHeight(), () =>
        {
            ClearLayoutTransitions();

            if (isRunning)
            {
                SetInteractionEnabled(true);
            }
        });
    }

    public void AnimateOutward(Action completed)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => AnimateOutward(completed));
            return;
        }

        if (!isRunning)
        {
            completed();
            return;
        }

        spacingProgress = 0;
        SetInteractionEnabled(false);
        BeginAnimateOutward(completed);
    }

    public void Deactivate()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        cursorConfinement.Release();
        ShortcutHintsFlyout.Hide();
        ApplicationPickerFlyout.Hide();
        applicationLaunchCancellation?.Cancel();
        applicationLaunchCancellation?.Dispose();
        applicationLaunchCancellation = null;
        SetInteractionEnabled(false);
        WindowSearchBox.Text = string.Empty;
        ClearWindowSnapTarget();

        animator.Reset(PreviewSurface, GetAnimationWidth(), GetAnimationHeight());

        UnsubscribeEvents();
        pageStrip.Stop();

        Opacity = 0;
    }

    private void Synchronise()
    {
        if (!isRunning)
        {
            return;
        }

        RefreshMonitorOrigin();
        IReadOnlyList<TrackedWindow> windows = previews.Synchronise(PreviewCanvas, FocusCanvas, windowCollection.AllTrackedWindows, animator.Scale);

        pageStrip.Synchronise(scroller.VisualOffset);

        foreach (TrackedWindow trackedWindow in windows)
        {
            if (previews.TryGet(trackedWindow.Handle, out DesktopWindowPreview? preview) && preview is not null)
            {
                UpdateWindowLayout(trackedWindow, preview, null);
            }
        }
    }

    private void RefreshLayout(TimeSpan? transitionDuration = null)
    {
        if (!isRunning)
        {
            return;
        }

        RefreshMonitorOrigin();
        pageStrip.RefreshLayout(scroller.VisualOffset, spacingProgress, transitionDuration);

        foreach (TrackedWindow trackedWindow in windowCollection.AllTrackedWindows)
        {
            if (!previews.TryGet(trackedWindow.Handle, out DesktopWindowPreview? preview) || preview is null)
            {
                continue;
            }

            UpdateWindowLayout(trackedWindow, preview, transitionDuration);
        }

        RefreshActiveWindowSnapTarget();
    }

    private void UpdateWindowLayout(TrackedWindow trackedWindow, DesktopWindowPreview preview, TimeSpan? transitionDuration)
    {
        ShellWindowLayout layout = layoutCalculator.Calculate(trackedWindow, scroller.VisualOffset, monitorOriginX, monitorOriginY, 1, workspace.Width, workspace.Height);
        double x = pageLayoutCalculator.CalculateWindowX(layout.X, trackedWindow.CanvasX, trackedWindow.Width, monitorOriginX, workspace.Width, scroller.VisualOffset, spacingProgress);

        TimeSpan? effectiveTransition = transitionDuration;

        if (pageReorderState is not null && workspace.Width > 0)
        {
            int page = PageReorderMapping.GetPage(trackedWindow, workspace.Width);

            if (page == pageReorderState.SourcePage)
            {
                x += pageReorderState.HorizontalDelta;
                effectiveTransition = null;
            }
            else
            {
                int reorderedPage = pageReorderState.MapPage(page);
                x += (reorderedPage - page) * (workspace.Width + (pageLayoutCalculator.PageSpacing * spacingProgress));
            }
        }

        preview.Update(x, layout.Y, preview.SourceWidth, preview.SourceHeight, effectiveTransition);
    }

    private void ClearLayoutTransitions()
    {
        pageStrip.ClearTranslationTransitions();
        previews.ClearTranslationTransitions();
    }

    private void SetInteractionEnabled(bool value)
    {
        DismissSurface.IsHitTestVisible = value;
        WindowSearchBox.IsHitTestVisible = value;
        WindowSearchBox.IsTabStop = value;
        ShortcutHintSurface.IsHitTestVisible = value;
        ShortcutHintSurface.IsTabStop = value;
        SettingsButton.IsHitTestVisible = value;
        SettingsButton.IsTabStop = value;
        pageStrip.SetInteractionEnabled(value);
        previews.SetInteractionEnabled(value);

        if (value)
        {
            _ = WindowSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
    }

    private void BeginAnimateOutward(Action completed)
    {
        if (!isRunning)
        {
            completed();
            return;
        }

        RefreshLayout(animator.ExitDuration);

        animator.AnimateOutward(PreviewSurface, GetAnimationWidth(), GetAnimationHeight(), () =>
        {
            ClearLayoutTransitions();
            completed();
        });
    }

    private double GetAnimationWidth() => ActualWidth > 0 ? ActualWidth : XamlRoot?.Size.Width ?? workspace.Width;

    private double GetAnimationHeight() => workspace.Height > 0 ? workspace.Height : ActualHeight > 0 ? ActualHeight : XamlRoot?.Size.Height ?? 0;

    private bool RefreshMonitorOrigin()
    {
        int x = workspace.WorkAreaX;
        int y = workspace.WorkAreaY;
        int offsetY = Math.Max(0, y - overlayScreenOriginY);
        bool changed = monitorOriginX != x || monitorOriginY != y || workAreaOffsetY != offsetY;
        monitorOriginX = x;
        monitorOriginY = y;
        workAreaOffsetY = offsetY;
        PreviewSurface.Translation = new Vector3(0, workAreaOffsetY, 0);
        ShortcutHintSurface.Margin = new Thickness(0, Math.Max(0, workAreaOffsetY + workspace.Height - 60), 0, 0);
        pageStrip.SetWorkAreaOffsetY(workAreaOffsetY);
        cursorConfinement.SetWorkAreaOffsetY(workAreaOffsetY);
        return changed;
    }

    private void SubscribeEvents()
    {
        if (eventsSubscribed)
        {
            return;
        }

        eventsSubscribed = true;
        panState.OffsetChanged += HandleOffsetChanged;
        windowCollection.WindowAdded += HandleCollectionChanged;
        windowCollection.WindowRemoved += HandleRemoved;
        windowCollection.WindowChanged += HandleChanged;
        windowCollection.WindowStackRefreshed += HandleSynchroniseRequested;
        windowCollection.WorkspaceLayoutChanged += HandleSynchroniseRequested;
        windowCollection.RefreshRequested += HandleLayoutRefreshRequested;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed)
        {
            return;
        }

        eventsSubscribed = false;
        panState.OffsetChanged -= HandleOffsetChanged;
        windowCollection.WindowAdded -= HandleCollectionChanged;
        windowCollection.WindowRemoved -= HandleRemoved;
        windowCollection.WindowChanged -= HandleChanged;
        windowCollection.WindowStackRefreshed -= HandleSynchroniseRequested;
        windowCollection.WorkspaceLayoutChanged -= HandleSynchroniseRequested;
        windowCollection.RefreshRequested -= HandleLayoutRefreshRequested;
    }

    private void HandleCollectionChanged(object? sender, TrackedWindow trackedWindow) => QueueSynchronise();

    private void HandleRemoved(object? sender, nint handle) => QueueSynchronise();

    private void HandleChanged(object? sender, TrackedWindow trackedWindow) => QueueWindowRefresh(trackedWindow);

    private void HandleSynchroniseRequested(object? sender, EventArgs args) => QueueSynchronise();

    private void HandleLayoutRefreshRequested(object? sender, EventArgs args) => QueueLayoutRefresh();

    private void HandleOffsetChanged()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplicationPickerFlyout.Hide();
            RefreshLayout();
        }
        else
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplicationPickerFlyout.Hide();
                RefreshLayout();
            });
        }
    }

    private void HandleDismissSurfaceTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;

        Dismiss();
    }

    private void HandleSettingsButtonClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs args)
    {
        ResetFilter();
        SetInteractionEnabled(false);

        SettingsInvoked?.Invoke(this, EventArgs.Empty);
    }

    private void HandlePageInvoked(int page)
    {
        ResetFilter();
        SetInteractionEnabled(false);

        PageInvoked?.Invoke(page);
    }

    private void HandlePageReorderPreviewChanged(DesktopPageReorderPreviewState? state, TimeSpan? transitionDuration)
    {
        pageReorderState = state;
        IReadOnlyList<TrackedWindow> windows = [.. windowCollection.AllTrackedWindows];

        previews.SetPageReorderState(state, windows, workspace.Width);

        foreach (TrackedWindow window in windows)
        {
            if (state is not null && !transitionDuration.HasValue && PageReorderMapping.GetPage(window, workspace.Width) != state.SourcePage)
            {
                continue;
            }

            if (previews.TryGet(window.Handle, out DesktopWindowPreview? preview) && preview is not null)
            {
                UpdateWindowLayout(window, preview, transitionDuration);
            }
        }
    }

    private void HandleWindowInvoked(nint handle)
    {
        ResetFilter();
        SetInteractionEnabled(false);

        WindowInvoked?.Invoke(handle);
    }

    private void HandleWindowPositionChanged(nint handle)
    {
        if (isRunning && windowCollection.TryGetTrackedWindow(handle, out TrackedWindow? trackedWindow) && trackedWindow is not null && previews.TryGet(handle, out DesktopWindowPreview? preview) && preview is not null)
        {
            previews.Refresh(trackedWindow);
            UpdateWindowLayout(trackedWindow, preview, null);
        }
    }

    private async void HandleApplicationPickerRequested(DesktopApplicationPickerRequest request)
    {
        if (!isRunning)
        {
            return;
        }

        try
        {
            await ApplicationPicker.LoadAsync(request.Target);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!isRunning)
                {
                    return;
                }

                ApplicationPickerFlyout.ShowAt(request.Anchor);
                _ = ApplicationSearchBox.Focus(FocusState.Programmatic);
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void HandleApplicationClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not DesktopApplicationPickerItemViewModel item || !isRunning)
        {
            return;
        }

        LaunchableApplication application = item.Application;
        DesktopApplicationTarget target = ApplicationPicker.Target;
        ApplicationPickerFlyout.Hide();
        applicationLaunchCancellation?.Cancel();
        applicationLaunchCancellation?.Dispose();
        applicationLaunchCancellation = new CancellationTokenSource();
        SetInteractionEnabled(false);

        try
        {
            await applicationLaunchCoordinator.LaunchAsync(application, target, monitorOriginX, monitorOriginY, applicationLaunchCancellation.Token);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (isRunning)
                {
                    SetInteractionEnabled(true);
                }
            });
        }
        catch (OperationCanceledException)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (isRunning)
                {
                    SetInteractionEnabled(true);
                }
            });
        }
    }

    private async void HandleApplicationContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is DesktopApplicationPickerItemViewModel item)
        {
            await ApplicationPicker.LoadIconAsync(item);
        }
    }

    private void HandleWindowSearchBoxTextChanged(object sender, TextChangedEventArgs args)
    {
        bool isActive = !string.IsNullOrWhiteSpace(WindowSearchBox.Text);
        IReadOnlyList<TrackedWindow> windows = [.. windowCollection.AllTrackedWindows];
        nint selectedHandle = previews.SetFilter(WindowSearchBox.Text, windows);

        if (!isRunning)
        {
            filterActive = false;
            pageBeforeFilter = -1;
            return;
        }

        if (!filterActive && isActive)
        {
            pageBeforeFilter = pager.CurrentPage;
        }

        filterActive = isActive;

        if (!isActive)
        {
            if (pageBeforeFilter >= 0 && !pager.IsPageCentered(pageBeforeFilter))
            {
                pager.NavigateToPage(pageBeforeFilter);
            }

            pageBeforeFilter = -1;
            return;
        }

        NavigateToFilterMatch(selectedHandle);
    }

    private void HandleWindowSearchBoxKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Shift)
        {
            shiftKeyDown = true;
            return;
        }

        if (args.Key == VirtualKey.Tab)
        {
            nint selectedHandle = previews.SelectNext(!shiftKeyDown, windowCollection.AllTrackedWindows);

            if (previews.Activate(selectedHandle))
            {
                InputFocusRequested?.Invoke(this, EventArgs.Empty);

                DispatcherQueue.TryEnqueue(() => _ = WindowSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic));
            }

            NavigateToFilterMatch(selectedHandle);
            args.Handled = true;
            return;
        }

        if (args.Key != VirtualKey.Enter)
        {
            return;
        }

        nint handle = previews.GetSelectedMatchingWindow(windowCollection.AllTrackedWindows);

        if (handle != 0)
        {
            args.Handled = true;
            HandleWindowInvoked(handle);
        }
    }

    private void HandleWindowSearchBoxKeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Shift)
        {
            shiftKeyDown = false;
        }
    }

    public void Dismiss()
    {
        ResetFilter();
        SetInteractionEnabled(false);

        BackgroundInvoked?.Invoke(this, EventArgs.Empty);
    }

    private void HandleShortcutHintsInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!isRunning || !ShortcutHintSurface.IsHitTestVisible)
        {
            return;
        }

        ShortcutHintsFlyout.ShowAt(ShortcutHintSurface);
        args.Handled = true;
    }

    public bool TryCancelEditor() => pageStrip.TryCancelEditor();

    private void ResetFilter()
    {
        filterActive = false;
        pageBeforeFilter = -1;
        shiftKeyDown = false;

        if (WindowSearchBox.Text.Length > 0)
        {
            WindowSearchBox.Text = string.Empty;
        }

        previews.SetFilter(string.Empty, windowCollection.AllTrackedWindows);
    }

    private void NavigateToFilterMatch(nint handle)
    {
        if (handle == 0 || workspace.Width <= 0 || !windowCollection.TryGetTrackedWindow(handle, out TrackedWindow? trackedWindow) || trackedWindow is null)
        {
            return;
        }

        double calculatedPage = Math.Floor((trackedWindow.CanvasX + (trackedWindow.Width / 2.0)) / workspace.Width);
        int page = (int)Math.Clamp(calculatedPage, 0, int.MaxValue);

        if (!pager.IsPageCentered(page))
        {
            pager.NavigateToPage(page);
        }
    }

    private void QueueSynchronise()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            Synchronise();
        }
        else
        {
            DispatcherQueue.TryEnqueue(Synchronise);
        }
    }

    private void QueueLayoutRefresh()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshLayout();
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => RefreshLayout());
        }
    }

    private void QueueWindowRefresh(TrackedWindow trackedWindow)
    {
        void RefreshWindow()
        {
            if (isRunning && previews.TryGet(trackedWindow.Handle, out _))
            {
                previews.Refresh(trackedWindow);
                previews.RefreshSelection(windowCollection.AllTrackedWindows);
                RefreshLayout();
            }
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshWindow();
        }
        else
        {
            DispatcherQueue.TryEnqueue(RefreshWindow);
        }
    }

    private void HandleWindowDragMoved(nint handle, double pointerX, double pointerY)
    {
        activeSnapWindow = handle;
        activeSnapPointerX = pointerX;
        activeSnapPointerY = pointerY;
        RefreshActiveWindowSnapTarget();
    }

    private void HandleWindowDragCompleted(nint handle)
    {
        if (activeSnapWindow == handle)
        {
            ClearWindowSnapTarget();
        }
    }

    private void RefreshActiveWindowSnapTarget()
    {
        if (!overviewConfiguration.IsSnapAssistanceEnabled)
        {
            ClearWindowSnapTarget();
            return;
        }

        if (activeSnapWindow == 0 ||
            !pageStrip.TryUpdateWindowSnapTarget(activeSnapPointerX, activeSnapPointerY, out DesktopSnapSlotTarget target) ||
            !snapPlacementResolver.TryResolve(target.Page, target.Layout, target.Slot, monitorOriginX, monitorOriginY, out DesktopSnapPlacement placement))
        {
            if (activeSnapWindow != 0)
            {
                previews.SetSnapTarget(activeSnapWindow, null);
            }

            pageStrip.ClearWindowSnapTarget();
            return;
        }

        snapSlotOccupancyResolver.TryGetOccupant(placement, activeSnapWindow, windowCollection.AllTrackedWindows, out TrackedWindow? occupant);
        previews.SetSnapTarget(activeSnapWindow, new DesktopWindowSnapTarget(placement, occupant?.Handle ?? 0));
    }

    private void ClearWindowSnapTarget()
    {
        if (activeSnapWindow != 0)
        {
            previews.SetSnapTarget(activeSnapWindow, null);
        }

        activeSnapWindow = 0;
        activeSnapPointerX = 0;
        activeSnapPointerY = 0;
        pageStrip.ClearWindowSnapTarget();
    }
}
