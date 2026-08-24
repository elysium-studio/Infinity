using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopScrollPreviewView :
    UserControl
{
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly IWindowCollection windowCollection;
    private readonly IShellLayoutCalculator layoutCalculator;
    private readonly IScroller scroller;
    private readonly IWorkspace workspace;
    private readonly ITaskbarLocator taskbarLocator;
    private readonly IWindowGeometryReader windowGeometryReader;
    private readonly DesktopScrollPreviewAnimator animator;
    private readonly ILogger<DesktopScrollPreviewView> logger;
    private readonly Dictionary<nint, DesktopWindowPreview> previews = [];
    private bool eventsSubscribed;
    private bool isRunning;
    private int monitorOriginX;
    private int monitorOriginY;

    public DesktopScrollPreviewView(IWindowPreviewSurface windowPreviewSurface,
        IWindowCollection windowCollection,
        IShellLayoutCalculator layoutCalculator,
        IScroller scroller,
        IWorkspace workspace,
        ITaskbarLocator taskbarLocator,
        IWindowGeometryReader windowGeometryReader,
        DesktopScrollPreviewAnimator animator,
        ILogger<DesktopScrollPreviewView> logger)
    {
        InitializeComponent();

        this.windowPreviewSurface = windowPreviewSurface;
        this.windowCollection = windowCollection;
        this.layoutCalculator = layoutCalculator;
        this.scroller = scroller;
        this.workspace = workspace;
        this.taskbarLocator = taskbarLocator;
        this.windowGeometryReader = windowGeometryReader;
        this.animator = animator;
        this.logger = logger;
    }

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
            SubscribeEvents();
            windowPreviewSurface.Initialize(ownerWindowHandle);
            Synchronise();
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

        animator.AnimateInward(PreviewCanvas, GetAnimationWidth(), GetAnimationHeight());
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

        animator.AnimateOutward(PreviewCanvas, GetAnimationWidth(), GetAnimationHeight(), completed);
    }

    public void Clear()
    {
        if (!isRunning && previews.Count == 0)
        {
            return;
        }

        isRunning = false;
        animator.Reset(PreviewCanvas, GetAnimationWidth(), GetAnimationHeight());
        UnsubscribeEvents();
        windowPreviewSurface.Clear();

        foreach (DesktopWindowPreview preview in previews.Values)
        {
            preview.Dispose();
        }

        previews.Clear();
        PreviewCanvas.Children.Clear();
        Opacity = 0;
    }

    private void Synchronise()
    {
        if (!isRunning)
        {
            return;
        }

        TrackedWindow[] windows = [.. windowCollection.AllTrackedWindows.OrderByDescending(window => window.ZIndex)];
        HashSet<nint> currentHandles = [.. windows.Select(window => window.Handle)];
        RefreshMonitorOrigin();

        foreach (nint handle in previews.Keys.Where(handle => !currentHandles.Contains(handle)).ToArray())
        {
            Remove(handle);
        }

        int zIndex = 0;

        foreach (TrackedWindow trackedWindow in windows)
        {
            if (!previews.TryGetValue(trackedWindow.Handle, out DesktopWindowPreview? preview))
            {
                Grid host = new()
                {
                    IsHitTestVisible = false
                };
                Border previewHost = new()
                {
                    Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                    IsHitTestVisible = false
                };

                host.Children.Add(previewHost);

                PreviewCanvas.Children.Add(host);
                ThumbnailCompositionPreview? compositionPreview = ThumbnailCompositionPreview.Create(windowPreviewSurface,
                    trackedWindow.Handle,
                    previewHost,
                    logger);

                preview = new DesktopWindowPreview(host, compositionPreview);
                previews.Add(trackedWindow.Handle, preview);
            }

            preview.RefreshSourceSize(trackedWindow, windowGeometryReader);

            ShellWindowLayout layout = layoutCalculator.Calculate(trackedWindow,
                scroller.VisualOffset,
                monitorOriginX,
                monitorOriginY,
                1,
                workspace.Width,
                workspace.Height);
            double x = trackedWindow.IsSticky
                ? trackedWindow.StickyViewportX - monitorOriginX
                : layout.X;

            preview.Update(x, layout.Y, preview.SourceWidth, preview.SourceHeight, zIndex++);
        }
    }

    private void RefreshLayout()
    {
        if (!isRunning)
        {
            return;
        }

        foreach (TrackedWindow trackedWindow in windowCollection.AllTrackedWindows)
        {
            if (!previews.TryGetValue(trackedWindow.Handle, out DesktopWindowPreview? preview))
            {
                continue;
            }

            ShellWindowLayout layout = layoutCalculator.Calculate(trackedWindow,
                scroller.VisualOffset,
                monitorOriginX,
                monitorOriginY,
                1,
                workspace.Width,
                workspace.Height);
            double x = trackedWindow.IsSticky
                ? trackedWindow.StickyViewportX - monitorOriginX
                : layout.X;

            preview.Update(x, layout.Y, preview.SourceWidth, preview.SourceHeight, null);
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
            DispatcherQueue.TryEnqueue(RefreshLayout);
        }
    }

    private void QueueWindowRefresh(TrackedWindow trackedWindow)
    {
        void RefreshWindow()
        {
            if (isRunning && previews.TryGetValue(trackedWindow.Handle, out DesktopWindowPreview? preview))
            {
                preview.RefreshSourceSize(trackedWindow, windowGeometryReader);
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

    private void Remove(nint handle)
    {
        if (!previews.Remove(handle, out DesktopWindowPreview? preview))
        {
            return;
        }

        preview.Dispose();
        PreviewCanvas.Children.Remove(preview.Host);
    }

}
