using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
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
    private readonly ITaskbarLocator taskbarLocator;
    private readonly DesktopPageLayoutCalculator pageLayoutCalculator;
    private readonly DesktopScrollPreviewAnimator animator;
    private readonly DesktopPageStrip pageStrip;
    private readonly DesktopWindowPreviewCollection previews;
    private bool eventsSubscribed;
    private bool filterActive;
    private bool shiftKeyDown;
    private bool isRunning;
    private int pageBeforeFilter = -1;
    private double spacingProgress = 1;
    private int monitorOriginX;
    private int monitorOriginY;

    public DesktopScrollPreviewView(IWindowPreviewSurface windowPreviewSurface,
        IWindowCollection windowCollection,
        IShellLayoutCalculator layoutCalculator,
        IPanState panState,
        IScroller scroller,
        IPager pager,
        IWorkspace workspace,
        ITaskbarLocator taskbarLocator,
        DesktopPageLayoutCalculator pageLayoutCalculator,
        DesktopScrollPreviewAnimator animator,
        DesktopPageStrip pageStrip,
        DesktopWindowPreviewCollection previews)
    {
        InitializeComponent();

        this.windowPreviewSurface = windowPreviewSurface;
        this.windowCollection = windowCollection;
        this.layoutCalculator = layoutCalculator;
        this.panState = panState;
        this.scroller = scroller;
        this.pager = pager;
        this.workspace = workspace;
        this.taskbarLocator = taskbarLocator;
        this.pageLayoutCalculator = pageLayoutCalculator;
        this.animator = animator;
        this.pageStrip = pageStrip;
        this.previews = previews;
        this.pageStrip.PageInvoked += HandlePageInvoked;
        this.previews.WindowInvoked += HandleWindowInvoked;
        this.previews.WindowPositionChanged += HandleWindowPositionChanged;
    }

    public event EventHandler? BackgroundInvoked;

    public event Action<int>? PageInvoked;

    public event Action<nint>? WindowInvoked;

    public bool IsRunning => isRunning;

    public void Prepare(nint ownerWindowHandle)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => Prepare(ownerWindowHandle));
            return;
        }

        if (!isRunning)
        {
            isRunning = true;
            spacingProgress = 1;
            SubscribeEvents();
            windowPreviewSurface.Initialize(ownerWindowHandle);
            pageStrip.Start(PageCanvas, PreviewSurface, animator.Scale);
            Synchronise();
            SetInteractionEnabled(false);
            Opacity = 1;
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
        animator.AnimateInward(PreviewSurface,
            GetAnimationWidth(),
            GetAnimationHeight(),
            () =>
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
        RefreshLayout(animator.ExitDuration);
        animator.AnimateOutward(PreviewSurface, GetAnimationWidth(), GetAnimationHeight(), () =>
        {
            ClearLayoutTransitions();
            completed();
        });
    }

    public void Deactivate()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        SetInteractionEnabled(false);
        WindowSearchBox.Text = string.Empty;
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

        IReadOnlyList<TrackedWindow> windows = previews.Synchronise(PreviewCanvas,
            FocusCanvas,
            windowCollection.AllTrackedWindows,
            animator.Scale);
        RefreshMonitorOrigin();
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

        pageStrip.RefreshLayout(scroller.VisualOffset, spacingProgress, transitionDuration);

        foreach (TrackedWindow trackedWindow in windowCollection.AllTrackedWindows)
        {
            if (!previews.TryGet(trackedWindow.Handle, out DesktopWindowPreview? preview) || preview is null)
            {
                continue;
            }

            UpdateWindowLayout(trackedWindow, preview, transitionDuration);
        }
    }

    private void UpdateWindowLayout(TrackedWindow trackedWindow,
        DesktopWindowPreview preview,
        TimeSpan? transitionDuration)
    {
        ShellWindowLayout layout = layoutCalculator.Calculate(trackedWindow,
            scroller.VisualOffset,
            monitorOriginX,
            monitorOriginY,
            1,
            workspace.Width,
            workspace.Height);
        double x = pageLayoutCalculator.CalculateWindowX(layout.X,
            trackedWindow.CanvasX,
            trackedWindow.Width,
            monitorOriginX,
            workspace.Width,
            scroller.VisualOffset,
            spacingProgress);

        preview.Update(x,
            layout.Y,
            preview.SourceWidth,
            preview.SourceHeight,
            transitionDuration);
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
        pageStrip.SetInteractionEnabled(value);
        previews.SetInteractionEnabled(value);

        if (value)
        {
            _ = WindowSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
    }

    private void RefreshMonitorOrigin()
    {
        MonitorHandle monitor = new(workspace.GetCurrentWorkspace());
        TaskbarInfo? taskbar = taskbarLocator.GetTaskbarForMonitor(monitor);
        monitorOriginX = taskbar?.MonitorBounds.Left ?? workspace.WorkAreaX;
        monitorOriginY = taskbar?.MonitorBounds.Top ?? workspace.WorkAreaY;
    }

    private double GetAnimationWidth() => ActualWidth > 0 ? ActualWidth : XamlRoot?.Size.Width ?? workspace.Width;

    private double GetAnimationHeight() => ActualHeight > 0 ? ActualHeight : XamlRoot?.Size.Height ?? workspace.Height;

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

    private void HandleOffsetChanged() => QueueLayoutRefresh();

    private void HandleDismissSurfaceTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        Dismiss();
    }

    private void HandleEscapeInvoked(KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!isRunning)
        {
            return;
        }

        args.Handled = true;
        Dismiss();
    }

    private void HandlePageInvoked(int page)
    {
        ResetFilter();
        SetInteractionEnabled(false);
        PageInvoked?.Invoke(page);
    }

    private void HandleWindowInvoked(nint handle)
    {
        ResetFilter();
        SetInteractionEnabled(false);
        WindowInvoked?.Invoke(handle);
    }

    private void HandleWindowPositionChanged(nint handle)
    {
        if (isRunning &&
            windowCollection.TryGetTrackedWindow(handle, out TrackedWindow? trackedWindow) &&
            trackedWindow is not null &&
            previews.TryGet(handle, out DesktopWindowPreview? preview) &&
            preview is not null)
        {
            UpdateWindowLayout(trackedWindow, preview, null);
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

    private void Dismiss()
    {
        ResetFilter();
        SetInteractionEnabled(false);
        BackgroundInvoked?.Invoke(this, EventArgs.Empty);
    }

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
        if (handle == 0 ||
            workspace.Width <= 0 ||
            !windowCollection.TryGetTrackedWindow(handle, out TrackedWindow? trackedWindow) ||
            trackedWindow is null)
        {
            return;
        }

        double calculatedPage = Math.Floor((trackedWindow.CanvasX + (trackedWindow.Width / 2.0)) /
            workspace.Width);
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
}
