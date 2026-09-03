using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Infinity.Shell.WinUI;

public sealed class DesktopPageStrip(IDesktopBackgroundSource backgroundSource, IPager pager, IScroller scroller, IWorkspace workspace, PageTitleStore pageTitleStore, PageLayoutStore pageLayoutStore, DesktopPageReorderController reorderController, DesktopPageArrangementCoordinator arrangementCoordinator, DesktopOverviewDragScroller overviewDragScroller, DesktopDragBoundaryCalculator dragBoundaryCalculator, DesktopDragCursorConfinement cursorConfinement, ITextLocalizer localizer, DesktopPageLayoutCalculator layoutCalculator, DesktopSnapLayoutCatalog snapLayoutCatalog, DesktopPageBackgroundFactory backgroundFactory, DesktopWallpaperPlacementCalculator wallpaperPlacementCalculator, DesktopWallpaperBrushFactory wallpaperBrushFactory, ILogger<DesktopPageStrip> logger) :
    IDisposable
{
    private static readonly TimeSpan ReorderAnimationDuration = TimeSpan.FromMilliseconds(180);

    private readonly Dictionary<int, DesktopPagePreview> visiblePages = [];
    private readonly List<DesktopPagePreview> pagePool = [];
    private readonly Stack<DesktopPagePreview> availablePages = [];
    private readonly DesktopPageEditorLabels editorLabels = new(localizer.GetText("PageTitleEditButton"), localizer.GetText("PageTitleSaveButton"), localizer.GetText("PageTitleCancelButton"), localizer.GetText("PageLayoutEditButton"), localizer.GetText("PageLayoutArrangeButton"), localizer.GetText("PageLayoutClearButton"));
    private Canvas? host;
    private Canvas? shadowHost;
    private Canvas? titleHost;
    private FrameworkElement? scaleHost;
    private DesktopPageBackground? background;
    private DesktopBackground? backgroundSnapshot;
    private DesktopPageDisplayState? displayState;
    private DesktopWallpaperPlacement wallpaperPlacement;
    private double currentOffset;
    private double currentSpacingProgress = 1;
    private double overviewScale;
    private double leadingSpace;
    private double reorderPointerDelta;
    private double reorderStartContentOffset;
    private double workAreaOffsetX;
    private double workAreaOffsetY;
    private DesktopPageReorderPreviewState? reorderState;
    private bool interactionEnabled;
    private int activeSnapPage = -1;
    private bool started;
    private bool disposed;

    public event Action<int>? PageInvoked;

    public event Action<DesktopPageReorderPreviewState?, TimeSpan?>? ReorderPreviewChanged;

    internal bool IsEditorActive => visiblePages.Values.Any(page => page.TitleEditor.ViewModel.IsEditing);

    internal int LastVisiblePage => visiblePages.Count == 0 ? pager.CurrentPage : visiblePages.Keys.Max();

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
        SetHeadersVisible(false);

        scaleHost.SizeChanged += HandleScaleHostSizeChanged;
        ConfigureHost();
        EnsurePagePoolCapacity();

        backgroundSource.BackgroundChanged += HandleBackgroundChanged;
        pageTitleStore.TitleChanged += HandlePageTitleChanged;
        pageLayoutStore.LayoutChanged += HandlePageLayoutChanged;
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
        SetHeadersVisible(false);
        overviewDragScroller.Stop();
        cursorConfinement.Release();

        backgroundSource.BackgroundChanged -= HandleBackgroundChanged;
        pageTitleStore.TitleChanged -= HandlePageTitleChanged;
        pageLayoutStore.LayoutChanged -= HandlePageLayoutChanged;
        overviewDragScroller.ScrollLimitReached -= HandleScrollLimitReached;

        if (scaleHost is not null)
        {
            scaleHost.SizeChanged -= HandleScaleHostSizeChanged;
        }

        foreach (DesktopPagePreview page in pagePool)
        {
            page.Click -= HandlePageClicked;
            page.DragStarted -= HandlePageDragStarted;
            page.DragMoved -= HandlePageDragMoved;
            page.DragCompleted -= HandlePageDragCompleted;
            page.DragCanceled -= HandlePageDragCanceled;
            page.TitleEditor.ViewModel.TitleSubmitted -= HandleTitleSubmitted;
            page.TitleEditor.ViewModel.LayoutSubmitted -= HandleLayoutSubmitted;
            page.TitleEditor.ViewModel.ArrangeRequested -= HandleArrangeRequested;
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
        activeSnapPage = -1;
        displayState = null;
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

        if (Math.Abs(offset - currentOffset) > 0.01)
        {
            foreach (DesktopPagePreview page in visiblePages.Values)
            {
                page.TitleEditor.CloseLayoutFlyout();
            }
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

    public void SetHeadersVisible(bool visible)
    {
        if (titleHost is null)
        {
            return;
        }

        ElementCompositionPreview.GetElementVisual(titleHost).Opacity = visible ? 1 : 0;
        titleHost.IsHitTestVisible = visible;
    }

    public void SetMonitorBounds(int x, int y, int width, int height)
    {
        wallpaperPlacement = wallpaperPlacementCalculator.Calculate(
            x,
            y,
            width,
            height,
            workspace.WorkAreaX,
            workspace.WorkAreaY);
    }

    public void SetWorkAreaOffset(double x, double y)
    {
        workAreaOffsetX = double.IsFinite(x) ? x : 0;
        workAreaOffsetY = double.IsFinite(y) ? y : 0;
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;
        bool canceledReorder = !value && reorderState is not null;

        if (!value)
        {
            ClearWindowSnapTarget();
        }

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

    internal bool TryUpdateWindowSnapTarget(double pointerX, double pointerY, out DesktopSnapSlotTarget target)
    {
        target = default;
        DesktopPagePreview? targetPage = visiblePages.Values.FirstOrDefault(page => pointerX >= page.ScreenX && pointerX <= page.ScreenX + page.ScreenWidth && pointerY >= page.ScreenY && pointerY <= page.ScreenY + page.ScreenHeight);

        if (targetPage is null)
        {
            ClearWindowSnapTarget();
            return false;
        }

        DesktopSnapLayoutKind layout = targetPage.TitleEditor.ViewModel.Layout;

        if (layout == DesktopSnapLayoutKind.None || targetPage.ScreenWidth <= 0 || targetPage.ScreenHeight <= 0)
        {
            ClearWindowSnapTarget();
            return false;
        }

        double normalizedX = (pointerX - targetPage.ScreenX) / targetPage.ScreenWidth;
        double normalizedY = (pointerY - targetPage.ScreenY) / targetPage.ScreenHeight;
        int slot = snapLayoutCatalog.HitTest(layout, normalizedX, normalizedY);

        if (slot < 0)
        {
            ClearWindowSnapTarget();
            return false;
        }

        if (activeSnapPage != targetPage.Page)
        {
            ClearWindowSnapTarget();
            activeSnapPage = targetPage.Page;
        }

        targetPage.ShowSnapZones(layout, slot);
        target = new DesktopSnapSlotTarget(targetPage.Page, layout, slot);
        return true;
    }

    internal void ClearWindowSnapTarget()
    {
        if (activeSnapPage >= 0 && visiblePages.TryGetValue(activeSnapPage, out DesktopPagePreview? page))
        {
            page.HideSnapZones();
        }

        activeSnapPage = -1;
    }

    internal bool TryCancelEditor()
    {
        DesktopPagePreview? page = visiblePages.Values.FirstOrDefault(page => page.TitleEditor.ViewModel.IsEditing);

        if (page is null)
        {
            return false;
        }

        page.TitleEditor.ViewModel.Cancel();

        return true;
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

    private void RefreshVisiblePages(TimeSpan? transitionDuration, bool preserveExistingBindings = false)
    {
        if (host is null || titleHost is null || background is null)
        {
            return;
        }

        (int firstPage, int lastPage) = layoutCalculator.CalculateVisiblePageRange(pager.MaxPages, currentOffset, workspace.Width, GetViewportWidth(), overviewScale, currentSpacingProgress);
        DesktopPageDisplayState nextDisplayState = new(workspace.Width, workspace.Height, GetRasterizationScale(), wallpaperPlacement);
        bool displayChanged = displayState != nextDisplayState;
        if (!preserveExistingBindings && reorderState is null)
        {
            displayState = nextDisplayState;
        }

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

                preview.Bind(page, workspace.Width, workspace.Height, background, wallpaperPlacement, pageTitleStore.GetTitle(page), pageLayoutStore.GetLayout(page), GetRasterizationScale());
                preview.SetInteractionEnabled(interactionEnabled);
                visiblePages.Add(page, preview);
            }
            else if (displayChanged && !preserveExistingBindings && reorderState is null)
            {
                preview.Bind(page, workspace.Width, workspace.Height, background, wallpaperPlacement, pageTitleStore.GetTitle(page), pageLayoutStore.GetLayout(page), GetRasterizationScale());
            }

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

        double viewportWidth = GetWorkAreaViewportWidth();
        double viewportHeight = GetViewportHeight();
        double pageScreenX = workAreaOffsetX + (viewportWidth / 2) + ((baseX - (viewportWidth / 2)) * overviewScale);
        double animationHeight = workspace.Height > 0 ? workspace.Height : viewportHeight;
        double pageScreenY = workAreaOffsetY + ((animationHeight / 2) * (1 - overviewScale));
        double titleX = pageScreenX + ((workspace.Width * overviewScale) - preview.TitleEditor.Width) / 2;
        double translationX = targetX - baseX;

        Canvas.SetLeft(preview.PageHost, leadingSpace + baseX);
        Canvas.SetTop(preview.PageHost, 0);
        Canvas.SetLeft(preview.ShadowHost, pageScreenX);
        Canvas.SetTop(preview.ShadowHost, pageScreenY);
        Canvas.SetLeft(preview.TitleEditor, titleX);
        Canvas.SetTop(preview.TitleEditor, pageScreenY - preview.TitleEditor.Height);
        preview.SetScreenBounds(pageScreenX, pageScreenY, workspace.Width * overviewScale, workspace.Height * overviewScale);

        TimeSpan? effectiveTransition = reorderState?.SourcePage == page ? null : transitionDuration;
        preview.Update(translationX, translationX * overviewScale, effectiveTransition);
    }

    private void ConfigureHost()
    {
        if (host is null || shadowHost is null || titleHost is null || overviewScale <= 0)
        {
            return;
        }

        double unscaledViewportWidth = GetViewportWidth() / overviewScale;
        leadingSpace = Math.Max(0, (unscaledViewportWidth - workspace.Width) / 2);

        host.Width = unscaledViewportWidth;
        host.Height = workspace.Height;
        Canvas.SetLeft(host, -leadingSpace);

        shadowHost.Width = GetViewportWidth();
        shadowHost.Height = GetViewportHeight();
        titleHost.Width = GetViewportWidth();
        titleHost.Height = GetViewportHeight();
    }

    private void EnsurePagePoolCapacity()
    {
        if (host is null || shadowHost is null || titleHost is null || scaleHost is null)
        {
            return;
        }

        int capacity = layoutCalculator.CalculateVisiblePageCapacity(GetViewportWidth(), workspace.Width, overviewScale);
        Visual scaleVisual = ElementCompositionPreview.GetElementVisual(scaleHost);

        for (int index = pagePool.Count; index < capacity; index++)
        {
            DesktopPagePreview page = new(scaleVisual, overviewScale, snapLayoutCatalog, wallpaperBrushFactory, editorLabels);

            page.Click += HandlePageClicked;
            page.DragStarted += HandlePageDragStarted;
            page.DragMoved += HandlePageDragMoved;
            page.DragCompleted += HandlePageDragCompleted;
            page.DragCanceled += HandlePageDragCanceled;
            page.TitleEditor.ViewModel.TitleSubmitted += HandleTitleSubmitted;
            page.TitleEditor.ViewModel.LayoutSubmitted += HandleLayoutSubmitted;
            page.TitleEditor.ViewModel.ArrangeRequested += HandleArrangeRequested;

            page.Hide();
            pagePool.Add(page);
            availablePages.Push(page);

            host.Children.Add(page.PageHost);
            shadowHost.Children.Add(page.ShadowHost);
            titleHost.Children.Add(page.TitleEditor);
        }
    }

    private double GetViewportWidth() => scaleHost?.ActualWidth > 0 ? scaleHost.ActualWidth : workspace.Width;

    private double GetWorkAreaViewportWidth() => workspace.Width > 0 ? workspace.Width : GetViewportWidth();

    private double GetViewportHeight() => scaleHost?.ActualHeight > 0 ? scaleHost.ActualHeight : workspace.Height;

    private double GetRasterizationScale() => scaleHost?.XamlRoot?.RasterizationScale ?? 1;

    private void HandleScaleHostSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (!started)
        {
            return;
        }

        ConfigureHost();
        EnsurePagePoolCapacity();
        RefreshVisiblePages(null);
    }

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
        bool reordered = false;

        if (failure is not null)
        {
            logger.LogError(failure, "Failed to reorder desktop page {SourcePage} to {TargetPage}", completedState.SourcePage, completedState.TargetPage);
        }
        else if (started && completedState.SourcePage != completedState.TargetPage && reorderedTitles is not null)
        {
            RemapVisiblePages(completedState.SourcePage, completedState.TargetPage, reorderedTitles);
            reordered = true;
        }

        reorderState = null;
        reorderPointerDelta = 0;
        reorderStartContentOffset = 0;

        if (started)
        {
            // RemapVisiblePages has already bound each preview to the page identity
            // that travelled with it. Do not immediately overwrite that binding from
            // an options monitor which may not have observed the persisted reorder yet.
            RefreshVisiblePages(ReorderAnimationDuration, preserveExistingBindings: reordered);
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

            preview.Bind(reorderedPage, workspace.Width, workspace.Height, background!, wallpaperPlacement, title, pageLayoutStore.GetLayout(reorderedPage), GetRasterizationScale());
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
            viewModel.Bind(viewModel.Page, pageTitleStore.GetTitle(viewModel.Page), pageLayoutStore.GetLayout(viewModel.Page));
        }
    }

    private async void HandleLayoutSubmitted(DesktopPageTitleViewModel viewModel, DesktopSnapLayoutKind layout)
    {
        try
        {
            await pageLayoutStore.UpdateAsync(viewModel.Page, layout);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to update the snap layout for desktop page {Page}", viewModel.Page);
            DesktopSnapLayoutKind configuredLayout = pageLayoutStore.GetLayout(viewModel.Page);
            viewModel.Bind(viewModel.Page, pageTitleStore.GetTitle(viewModel.Page), configuredLayout);
        }
        finally
        {
            if (visiblePages.TryGetValue(viewModel.Page, out DesktopPagePreview? preview))
            {
                preview.HideSnapZones();
            }
        }
    }

    private void HandleArrangeRequested(DesktopPageTitleViewModel viewModel)
    {
        if (!started || reorderState is not null || !viewModel.HasLayout)
        {
            return;
        }

        arrangementCoordinator.Arrange(viewModel.Page, viewModel.Layout, workspace.WorkAreaX, workspace.WorkAreaY);
        visiblePages.GetValueOrDefault(viewModel.Page)?.TitleEditor.CloseLayoutFlyout();
    }

    private void HandlePageTitleChanged(int page, string title)
    {
        void RefreshTitle()
        {
            if (started && reorderState is null && visiblePages.TryGetValue(page, out DesktopPagePreview? preview))
            {
                preview.TitleEditor.ViewModel.Bind(page, title, pageLayoutStore.GetLayout(page));
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

    private void HandlePageLayoutChanged(int page, DesktopSnapLayoutKind layout)
    {
        void RefreshLayout()
        {
            if (started && reorderState is null && visiblePages.TryGetValue(page, out DesktopPagePreview? preview))
            {
                preview.TitleEditor.ViewModel.Bind(page, pageTitleStore.GetTitle(page), layout);
            }
        }

        if (host?.DispatcherQueue.HasThreadAccess == true)
        {
            RefreshLayout();
        }
        else
        {
            host?.DispatcherQueue.TryEnqueue(RefreshLayout);
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

            if (background is not null && current == backgroundSnapshot)
            {
                return;
            }

            backgroundSnapshot = current;
            background = backgroundFactory.Create(current);

            if (background is not null)
            {
                foreach (DesktopPagePreview page in visiblePages.Values)
                {
                    page.Bind(page.Page, workspace.Width, workspace.Height, background, wallpaperPlacement, pageTitleStore.GetTitle(page.Page), pageLayoutStore.GetLayout(page.Page), GetRasterizationScale());
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
