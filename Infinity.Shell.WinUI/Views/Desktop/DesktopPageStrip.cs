using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Infinity.Shell.WinUI;

public sealed class DesktopPageStrip(IDesktopBackgroundSource backgroundSource, IPager pager, IScroller scroller, IWorkspace workspace, PageTitleStore pageTitleStore, DesktopPageReorderController reorderController, DesktopOverviewDragScroller overviewDragScroller, DesktopDragBoundaryCalculator dragBoundaryCalculator, DesktopDragCursorConfinement cursorConfinement, ITextLocalizer localizer, DesktopPageLayoutCalculator layoutCalculator, DesktopBackgroundBrushFactory backgroundBrushFactory, ILogger<DesktopPageStrip> logger) :
    IDisposable
{
    private static readonly TimeSpan ReorderAnimationDuration = TimeSpan.FromMilliseconds(180);

    private readonly Dictionary<int, DesktopPagePreview> visiblePages = [];
    private readonly List<DesktopPagePreview> pagePool = [];
    private readonly Stack<DesktopPagePreview> availablePages = [];
    private Canvas? host;
    private Canvas? shadowHost;
    private Canvas? titleHost;
    private FrameworkElement? scaleHost;
    private Brush? background;
    private DesktopBackground? backgroundSnapshot;
    private double currentOffset;
    private double currentSpacingProgress = 1;
    private double overviewScale;
    private double leadingSpace;
    private double reorderPointerDelta;
    private double reorderStartContentOffset;
    private DesktopPageReorderPreviewState? reorderState;
    private bool interactionEnabled;
    private bool started;
    private bool disposed;

    public event Action<int>? PageInvoked;

    public event Action<DesktopPageReorderPreviewState?, TimeSpan?>? ReorderPreviewChanged;

    public void Start(Canvas canvas, Canvas shadowCanvas, Canvas titleCanvas, FrameworkElement scaleElement, double scale)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (started)
        {
            return;
        }

        started = true;
        host = canvas;
        shadowHost = shadowCanvas;
        titleHost = titleCanvas;
        scaleHost = scaleElement;
        overviewScale = scale;
        interactionEnabled = false;

        ConfigureHost();
        CreatePagePool(scaleElement);

        backgroundSource.BackgroundChanged += HandleBackgroundChanged;
        pageTitleStore.TitleChanged += HandlePageTitleChanged;
        overviewDragScroller.ScrollLimitReached += HandleScrollLimitReached;

        RefreshBackground();
        Synchronise(scroller.VisualOffset);
    }

    public void Stop()
    {
        if (!started)
        {
            return;
        }

        started = false;
        overviewDragScroller.Stop();
        cursorConfinement.Release();

        backgroundSource.BackgroundChanged -= HandleBackgroundChanged;
        pageTitleStore.TitleChanged -= HandlePageTitleChanged;
        overviewDragScroller.ScrollLimitReached -= HandleScrollLimitReached;

        foreach (DesktopPagePreview page in pagePool)
        {
            page.Click -= HandlePageClicked;
            page.DragStarted -= HandlePageDragStarted;
            page.DragMoved -= HandlePageDragMoved;
            page.DragCompleted -= HandlePageDragCompleted;
            page.DragCanceled -= HandlePageDragCanceled;
            page.TitleEditor.ViewModel.TitleSubmitted -= HandleTitleSubmitted;
            page.Reset();
            page.Dispose();
        }

        visiblePages.Clear();
        availablePages.Clear();
        pagePool.Clear();

        host?.Children.Clear();
        shadowHost?.Children.Clear();
        titleHost?.Children.Clear();

        host = null;
        shadowHost = null;
        titleHost = null;
        scaleHost = null;
        reorderState = null;
        reorderPointerDelta = 0;
        reorderStartContentOffset = 0;
        interactionEnabled = false;
    }

    public void Synchronise(double offset)
    {
        if (!started)
        {
            return;
        }

        currentOffset = offset;
        currentSpacingProgress = 1;

        ConfigureHost();
        RefreshVisiblePages(null);
    }

    public void RefreshLayout(double offset, double spacingProgress = 1, TimeSpan? transitionDuration = null)
    {
        if (!started)
        {
            return;
        }

        currentOffset = offset;
        currentSpacingProgress = spacingProgress;
        TimeSpan? reorderTransition = null;

        if (reorderState is not null)
        {
            DesktopPageReorderPreviewState previousState = reorderState;
            UpdateReorderState(!overviewDragScroller.IsActive);
            reorderTransition = HasReorderLayoutChanged(previousState, reorderState) ? ReorderAnimationDuration : null;
        }

        ConfigureHost();
        RefreshVisiblePages(reorderTransition ?? transitionDuration);

        if (reorderState is not null)
        {
            ReorderPreviewChanged?.Invoke(reorderState, reorderTransition);
        }
    }

    public void ClearTranslationTransitions()
    {
        foreach (DesktopPagePreview page in visiblePages.Values)
        {
            page.ClearTranslationTransition();
        }
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;
        bool canceledReorder = !value && reorderState is not null;

        if (canceledReorder)
        {
            overviewDragScroller.Stop();
            cursorConfinement.Release();
            reorderState = null;
            reorderPointerDelta = 0;
            reorderStartContentOffset = 0;
        }

        foreach (DesktopPagePreview page in visiblePages.Values)
        {
            page.SetInteractionEnabled(value);
        }

        if (canceledReorder)
        {
            RefreshVisiblePages(null);
            ReorderPreviewChanged?.Invoke(null, null);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }

    private void RefreshVisiblePages(TimeSpan? transitionDuration)
    {
        if (host is null || background is null)
        {
            return;
        }

        (int firstPage, int lastPage) = layoutCalculator.CalculateVisiblePageRange(pager.MaxPages, currentOffset, workspace.Width, overviewScale, currentSpacingProgress);

        foreach ((int page, DesktopPagePreview preview) in visiblePages.ToArray())
        {
            if ((page < firstPage || page > lastPage) && reorderState?.SourcePage != page)
            {
                visiblePages.Remove(page);
                preview.Hide();
                availablePages.Push(preview);
            }
        }

        for (int page = firstPage; page <= lastPage; page++)
        {
            if (!visiblePages.TryGetValue(page, out DesktopPagePreview? preview))
            {
                preview = Acquire();

                if (preview is null)
                {
                    logger.LogWarning("Desktop page pool exhausted while presenting page {Page}", page);
                    continue;
                }

                preview.Bind(page, workspace.Width, workspace.Height, background, pageTitleStore.GetTitle(page));
                visiblePages.Add(page, preview);
            }
            else
            {
                preview.Bind(page, workspace.Width, workspace.Height, background, pageTitleStore.GetTitle(page));
            }

            preview.SetInteractionEnabled(interactionEnabled);
            UpdatePage(page, preview, transitionDuration);
        }

        if (reorderState is not null && (reorderState.SourcePage < firstPage || reorderState.SourcePage > lastPage) && visiblePages.TryGetValue(reorderState.SourcePage, out DesktopPagePreview? draggedPage))
        {
            UpdatePage(reorderState.SourcePage, draggedPage, null);
        }
    }

    private DesktopPagePreview? Acquire() => availablePages.Count > 0 ? availablePages.Pop() : null;

    private void UpdatePage(int page, DesktopPagePreview preview, TimeSpan? transitionDuration)
    {
        double fullContentOffset = layoutCalculator.CalculateContentOffset(currentOffset, workspace.Width);
        double baseX = page * (workspace.Width + layoutCalculator.PageSpacing) - fullContentOffset;
        double targetX = layoutCalculator.CalculatePageX(page, workspace.Width, currentOffset, currentSpacingProgress);

        if (reorderState is not null)
        {
            targetX = page == reorderState.SourcePage ? targetX + reorderState.HorizontalDelta : layoutCalculator.CalculatePageX(reorderState.MapPage(page), workspace.Width, currentOffset, currentSpacingProgress);
        }

        double viewportWidth = GetViewportWidth();
        double viewportHeight = GetViewportHeight();
        double pageScreenX = (viewportWidth / 2) + ((baseX - (viewportWidth / 2)) * overviewScale);
        double pageScreenY = (viewportHeight / 2) * (1 - overviewScale);
        double titleX = pageScreenX + ((workspace.Width * overviewScale) - preview.TitleEditor.Width) / 2;
        double translationX = targetX - baseX;

        Canvas.SetLeft(preview.PageHost, leadingSpace + baseX);
        Canvas.SetTop(preview.PageHost, 0);
        Canvas.SetLeft(preview.ShadowHost, pageScreenX);
        Canvas.SetTop(preview.ShadowHost, pageScreenY);
        Canvas.SetLeft(preview.TitleEditor, titleX);
        Canvas.SetTop(preview.TitleEditor, pageScreenY - preview.TitleEditor.Height);

        TimeSpan? effectiveTransition = reorderState?.SourcePage == page ? null : transitionDuration;
        preview.Update(translationX, translationX * overviewScale, effectiveTransition);
    }

    private void ConfigureHost()
    {
        if (host is null || shadowHost is null || titleHost is null || overviewScale <= 0)
        {
            return;
        }

        double viewportWidth = workspace.Width / overviewScale;
        leadingSpace = Math.Max(0, (viewportWidth - workspace.Width) / 2);

        host.Width = viewportWidth;
        host.Height = workspace.Height;
        Canvas.SetLeft(host, -leadingSpace);

        shadowHost.Width = GetViewportWidth();
        shadowHost.Height = GetViewportHeight();
        titleHost.Width = GetViewportWidth();
        titleHost.Height = GetViewportHeight();
    }

    private void CreatePagePool(FrameworkElement scaleHost)
    {
        if (host is null || shadowHost is null || titleHost is null)
        {
            return;
        }

        int capacity = layoutCalculator.CalculateVisiblePageCapacity(overviewScale);
        Visual scaleVisual = ElementCompositionPreview.GetElementVisual(scaleHost);

        for (int index = 0; index < capacity; index++)
        {
            DesktopPagePreview page = new(scaleVisual, overviewScale, localizer.GetText("PageTitleEditButton"), localizer.GetText("PageTitleSaveButton"), localizer.GetText("PageTitleCancelButton"));

            page.Click += HandlePageClicked;
            page.DragStarted += HandlePageDragStarted;
            page.DragMoved += HandlePageDragMoved;
            page.DragCompleted += HandlePageDragCompleted;
            page.DragCanceled += HandlePageDragCanceled;
            page.TitleEditor.ViewModel.TitleSubmitted += HandleTitleSubmitted;

            page.Hide();
            pagePool.Add(page);
            availablePages.Push(page);

            host.Children.Add(page.PageHost);
            shadowHost.Children.Add(page.ShadowHost);
            titleHost.Children.Add(page.TitleEditor);
        }
    }

    private double GetViewportWidth() => scaleHost?.ActualWidth > 0 ? scaleHost.ActualWidth : workspace.Width;

    private double GetViewportHeight() => scaleHost?.ActualHeight > 0 ? scaleHost.ActualHeight : workspace.Height;

    private void HandlePageClicked(object sender, RoutedEventArgs args)
    {
        if (started && reorderState is null && sender is DesktopPagePreview { IsDragging: false } page)
        {
            PageInvoked?.Invoke(page.Page);
        }
    }

    private void HandlePageDragStarted(DesktopPagePreview page)
    {
        if (!started || reorderState is not null)
        {
            return;
        }

        reorderState = new(page.Page, page.Page, 0);
        reorderPointerDelta = 0;
        reorderStartContentOffset = layoutCalculator.CalculateContentOffset(currentOffset, workspace.Width, currentSpacingProgress);
        cursorConfinement.Begin(GetViewportWidth(), GetViewportHeight(), overviewScale, scaleHost?.XamlRoot?.RasterizationScale ?? 1, constrainVertical: false);

        ReorderPreviewChanged?.Invoke(reorderState, null);
    }

    private void HandlePageDragMoved(DesktopPagePreview page, double horizontalDelta, double pointerX)
    {
        if (!started || reorderState?.SourcePage != page.Page || overviewScale <= 0)
        {
            return;
        }

        double viewportWidth = GetViewportWidth();
        double constrainedPointerX = dragBoundaryCalculator.ConstrainHorizontal(pointerX, viewportWidth, overviewScale);
        reorderPointerDelta = horizontalDelta + constrainedPointerX - pointerX;
        overviewDragScroller.Update(page.DispatcherQueue, constrainedPointerX, viewportWidth);
        cursorConfinement.Update(viewportWidth, GetViewportHeight(), overviewScale, scaleHost?.XamlRoot?.RasterizationScale ?? 1);

        DesktopPageReorderPreviewState previousState = reorderState;
        UpdateReorderState(!overviewDragScroller.IsActive);

        bool layoutChanged = HasReorderLayoutChanged(previousState, reorderState);
        TimeSpan? transitionDuration = layoutChanged ? ReorderAnimationDuration : null;

        if (layoutChanged)
        {
            RefreshVisiblePages(transitionDuration);
        }
        else if (visiblePages.TryGetValue(page.Page, out DesktopPagePreview? draggedPage))
        {
            UpdatePage(page.Page, draggedPage, null);
        }

        ReorderPreviewChanged?.Invoke(reorderState, transitionDuration);
    }

    private async void HandlePageDragCompleted(DesktopPagePreview page)
    {
        DesktopPageReorderPreviewState? currentState = reorderState;

        if (currentState is null || !started || currentState.SourcePage != page.Page)
        {
            return;
        }

        var dispatcherQueue = page.DispatcherQueue;
        overviewDragScroller.Stop();
        cursorConfinement.Release();
        UpdateReorderState(true);

        DesktopPageReorderPreviewState completedState = reorderState ?? currentState;

        IReadOnlyDictionary<int, string>? reorderedTitles = null;
        Exception? failure = null;

        try
        {
            if (completedState.SourcePage != completedState.TargetPage)
            {
                reorderedTitles = await reorderController.ReorderAsync(completedState.SourcePage, completedState.TargetPage);
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        void CompleteDrop() => CompletePageDrag(completedState, reorderedTitles, failure);

        if (dispatcherQueue.HasThreadAccess)
        {
            CompleteDrop();
        }
        else if (!dispatcherQueue.TryEnqueue(CompleteDrop))
        {
            logger.LogWarning("Could not return to the UI thread after reordering desktop page {SourcePage} to {TargetPage}", completedState.SourcePage, completedState.TargetPage);
        }
    }

    private void CompletePageDrag(DesktopPageReorderPreviewState completedState, IReadOnlyDictionary<int, string>? reorderedTitles, Exception? failure)
    {
        if (failure is not null)
        {
            logger.LogError(failure, "Failed to reorder desktop page {SourcePage} to {TargetPage}", completedState.SourcePage, completedState.TargetPage);
        }
        else if (started && completedState.SourcePage != completedState.TargetPage && reorderedTitles is not null)
        {
            RemapVisiblePages(completedState.SourcePage, completedState.TargetPage, reorderedTitles);
        }

        reorderState = null;
        reorderPointerDelta = 0;
        reorderStartContentOffset = 0;

        if (started)
        {
            RefreshVisiblePages(ReorderAnimationDuration);
            ReorderPreviewChanged?.Invoke(null, ReorderAnimationDuration);
        }
    }

    private void HandlePageDragCanceled(DesktopPagePreview page)
    {
        if (reorderState?.SourcePage != page.Page)
        {
            return;
        }

        reorderState = null;
        reorderPointerDelta = 0;
        reorderStartContentOffset = 0;

        overviewDragScroller.Stop();
        cursorConfinement.Release();
        RefreshVisiblePages(ReorderAnimationDuration);

        ReorderPreviewChanged?.Invoke(null, ReorderAnimationDuration);
    }

    private void HandleScrollLimitReached()
    {
        if (reorderState is null)
        {
            return;
        }

        DesktopPageReorderPreviewState previousState = reorderState;
        UpdateReorderState(true);

        TimeSpan? transitionDuration = HasReorderLayoutChanged(previousState, reorderState) ? ReorderAnimationDuration : null;

        RefreshVisiblePages(transitionDuration);
        ReorderPreviewChanged?.Invoke(reorderState, transitionDuration);
    }

    private void UpdateReorderState(bool createGap)
    {
        if (reorderState is null || overviewScale <= 0)
        {
            return;
        }

        double currentContentOffset = layoutCalculator.CalculateContentOffset(currentOffset, workspace.Width, currentSpacingProgress);
        double logicalDelta = (reorderPointerDelta / overviewScale) + (currentContentOffset - reorderStartContentOffset);
        double stride = workspace.Width + layoutCalculator.PageSpacing;

        double roundedPageDelta = stride > 0 ? Math.Round(logicalDelta / stride, MidpointRounding.AwayFromZero) : 0;

        long targetPage = reorderState.SourcePage + (long)Math.Clamp(roundedPageDelta, int.MinValue, int.MaxValue);
        long maximumPage = pager.MaxPages.HasValue ? Math.Max(0, pager.MaxPages.Value - 1L) : int.MaxValue;
        int clampedTargetPage = (int)Math.Clamp(targetPage, 0, maximumPage);
        reorderState = new(reorderState.SourcePage, clampedTargetPage, logicalDelta, createGap);
    }

    private static bool HasReorderLayoutChanged(DesktopPageReorderPreviewState previousState, DesktopPageReorderPreviewState currentState)
    {
        if (previousState.IsGapOpen != currentState.IsGapOpen)
        {
            return true;
        }

        if (currentState.IsGapOpen)
        {
            return previousState.TargetPage != currentState.TargetPage;
        }

        return Math.Sign(previousState.TargetPage - previousState.SourcePage) != Math.Sign(currentState.TargetPage - currentState.SourcePage);
    }

    private void RemapVisiblePages(int sourcePage, int targetPage, IReadOnlyDictionary<int, string> reorderedTitles)
    {
        KeyValuePair<int, DesktopPagePreview>[] pages = [.. visiblePages];
        visiblePages.Clear();

        foreach ((int page, DesktopPagePreview preview) in pages)
        {
            int reorderedPage = PageReorderMapping.Map(page, sourcePage, targetPage);
            string title = reorderedTitles.TryGetValue(reorderedPage, out string? reorderedTitle) ? reorderedTitle : pageTitleStore.GetTitle(reorderedPage);

            preview.Bind(reorderedPage, workspace.Width, workspace.Height, background!, title);
            visiblePages[reorderedPage] = preview;
        }
    }

    private async void HandleTitleSubmitted(DesktopPageTitleViewModel viewModel, string title)
    {
        try
        {
            await pageTitleStore.UpdateAsync(viewModel.Page, title);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to update the title for desktop page {Page}", viewModel.Page);
            viewModel.Bind(viewModel.Page, pageTitleStore.GetTitle(viewModel.Page));
        }
    }

    private void HandlePageTitleChanged(int page, string title)
    {
        void RefreshTitle()
        {
            if (started && reorderState is null && visiblePages.TryGetValue(page, out DesktopPagePreview? preview))
            {
                preview.TitleEditor.ViewModel.Bind(page, title);
            }
        }

        if (host?.DispatcherQueue.HasThreadAccess == true)
        {
            RefreshTitle();
        }
        else
        {
            host?.DispatcherQueue.TryEnqueue(RefreshTitle);
        }
    }

    private void HandleBackgroundChanged(object? sender, EventArgs args) => QueueBackgroundRefresh();

    private void QueueBackgroundRefresh()
    {
        Canvas? currentHost = host;

        if (currentHost is null)
        {
            return;
        }

        if (currentHost.DispatcherQueue.HasThreadAccess)
        {
            RefreshBackground();
        }
        else
        {
            currentHost.DispatcherQueue.TryEnqueue(RefreshBackground);
        }
    }

    private void RefreshBackground()
    {
        if (!started)
        {
            return;
        }

        try
        {
            DesktopBackground current = backgroundSource.GetBackground();

            if (background is null || current != backgroundSnapshot)
            {
                backgroundSnapshot = current;
                background = backgroundBrushFactory.Create(current);
            }

            if (background is not null)
            {
                foreach (DesktopPagePreview page in visiblePages.Values)
                {
                    page.Bind(page.Page, workspace.Width, workspace.Height, background, pageTitleStore.GetTitle(page.Page));
                    page.SetInteractionEnabled(interactionEnabled);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to refresh desktop page backgrounds");
        }
    }

}
