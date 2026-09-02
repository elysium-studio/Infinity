using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewLayoutPresenter(
    IWindowCollection windowCollection,
    IShellLayoutCalculator layoutCalculator,
    IScroller scroller,
    IWorkspace workspace,
    DesktopPageLayoutCalculator pageLayoutCalculator,
    DesktopPageStrip pageStrip,
    DesktopWindowPreviewCollection previews)
{
    private DesktopPageReorderPreviewState? pageReorderState;

    public void Synchronise(
        Canvas previewBackgroundCanvas,
        Canvas previewCanvas,
        Canvas focusCanvas,
        double scale,
        int monitorOriginX,
        int monitorOriginY,
        double spacingProgress)
    {
        IReadOnlyList<TrackedWindow> windows = previews.Synchronise(
            previewBackgroundCanvas,
            previewCanvas,
            focusCanvas,
            windowCollection.AllTrackedWindows,
            scale);

        pageStrip.Synchronise(scroller.VisualOffset);

        foreach (TrackedWindow trackedWindow in windows)
        {
            UpdateWindow(trackedWindow, monitorOriginX, monitorOriginY, spacingProgress, null);
        }

        previews.RefreshGroupStack();
    }

    public void Refresh(
        int monitorOriginX,
        int monitorOriginY,
        double spacingProgress,
        TimeSpan? transitionDuration = null)
    {
        pageStrip.RefreshLayout(scroller.VisualOffset, spacingProgress, transitionDuration);

        foreach (TrackedWindow trackedWindow in windowCollection.AllTrackedWindows)
        {
            UpdateWindow(trackedWindow, monitorOriginX, monitorOriginY, spacingProgress, transitionDuration);
        }

        previews.RefreshGroupStack();
    }

    public void RefreshWindow(
        nint handle,
        int monitorOriginX,
        int monitorOriginY,
        double spacingProgress)
    {
        if (windowCollection.TryGetTrackedWindow(handle, out TrackedWindow? trackedWindow) && trackedWindow is not null)
        {
            previews.Refresh(trackedWindow);
            UpdateWindow(trackedWindow, monitorOriginX, monitorOriginY, spacingProgress, null);
        }
    }

    public void SetPageReorderState(
        DesktopPageReorderPreviewState? state,
        int monitorOriginX,
        int monitorOriginY,
        double spacingProgress,
        TimeSpan? transitionDuration)
    {
        pageReorderState = state;
        IReadOnlyList<TrackedWindow> windows = [.. windowCollection.AllTrackedWindows];
        previews.SetPageReorderState(state, windows, workspace.Width);

        foreach (TrackedWindow window in windows)
        {
            if (state is not null &&
                !transitionDuration.HasValue &&
                PageReorderMapping.GetPage(window, workspace.Width) != state.SourcePage)
            {
                continue;
            }

            UpdateWindow(window, monitorOriginX, monitorOriginY, spacingProgress, transitionDuration);
        }
    }

    private void UpdateWindow(
        TrackedWindow trackedWindow,
        int monitorOriginX,
        int monitorOriginY,
        double spacingProgress,
        TimeSpan? transitionDuration)
    {
        if (!previews.TryGet(trackedWindow.Handle, out DesktopWindowPreview? preview) || preview is null)
        {
            return;
        }

        ShellWindowLayout layout = layoutCalculator.Calculate(
            trackedWindow,
            scroller.VisualOffset,
            monitorOriginX,
            monitorOriginY,
            1,
            workspace.Width,
            workspace.Height);
        double x = pageLayoutCalculator.CalculateWindowX(
            layout.X,
            trackedWindow.CanvasX,
            trackedWindow.Width,
            monitorOriginX,
            workspace.Width,
            scroller.VisualOffset,
            spacingProgress);
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

        preview.Update(x + preview.SourceOffsetX,
            layout.Y + preview.SourceOffsetY,
            preview.SourceWidth,
            preview.SourceHeight,
            effectiveTransition);
    }
}
