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

public sealed class DesktopPageStrip(IDesktopBackgroundSource backgroundSource,
    IPager pager,
    IScroller scroller,
    IWorkspace workspace,
    PageTitleStore pageTitleStore,
    ITextLocalizer localizer,
    DesktopPageLayoutCalculator layoutCalculator,
    DesktopBackgroundBrushFactory backgroundBrushFactory,
    ILogger<DesktopPageStrip> logger) :
    IDisposable
{
    private readonly Dictionary<int, DesktopPagePreview> visiblePages = [];
    private readonly List<DesktopPagePreview> pagePool = [];
    private readonly Stack<DesktopPagePreview> availablePages = [];
    private Canvas? host;
    private Canvas? titleHost;
    private FrameworkElement? scaleHost;
    private Brush? background;
    private DesktopBackground? backgroundSnapshot;
    private double currentOffset;
    private double currentSpacingProgress = 1;
    private double overviewScale;
    private double leadingSpace;
    private bool interactionEnabled;
    private bool started;
    private bool disposed;

    public event Action<int>? PageInvoked;

    public void Start(Canvas canvas,
        Canvas titleCanvas,
        FrameworkElement scaleElement,
        double scale)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (started)
        {
            return;
        }

        started = true;
        host = canvas;
        titleHost = titleCanvas;
        scaleHost = scaleElement;
        overviewScale = scale;
        interactionEnabled = false;
        ConfigureHost();
        CreatePagePool(scaleElement);
        backgroundSource.BackgroundChanged += HandleBackgroundChanged;
        pageTitleStore.TitleChanged += HandlePageTitleChanged;
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
        backgroundSource.BackgroundChanged -= HandleBackgroundChanged;
        pageTitleStore.TitleChanged -= HandlePageTitleChanged;

        foreach (DesktopPagePreview page in pagePool)
        {
            page.Click -= HandlePageClicked;
            page.TitleEditor.ViewModel.TitleSubmitted -= HandleTitleSubmitted;
            page.Reset();
            page.Dispose();
        }

        visiblePages.Clear();
        availablePages.Clear();
        pagePool.Clear();
        host?.Children.Clear();
        titleHost?.Children.Clear();
        host = null;
        titleHost = null;
        scaleHost = null;
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
        ConfigureHost();
        RefreshVisiblePages(transitionDuration);
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

        foreach (DesktopPagePreview page in visiblePages.Values)
        {
            page.SetInteractionEnabled(value);
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

        (int firstPage, int lastPage) = layoutCalculator.CalculateVisiblePageRange(pager.MaxPages,
            currentOffset,
            workspace.Width,
            overviewScale,
            currentSpacingProgress);

        foreach ((int page, DesktopPagePreview preview) in visiblePages.ToArray())
        {
            if (page < firstPage || page > lastPage)
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

                preview.Bind(page,
                    workspace.Width,
                    workspace.Height,
                    background,
                    pageTitleStore.GetTitle(page));
                visiblePages.Add(page, preview);
            }
            else
            {
                preview.Bind(page,
                    workspace.Width,
                    workspace.Height,
                    background,
                    pageTitleStore.GetTitle(page));
            }

            preview.SetInteractionEnabled(interactionEnabled);
            UpdatePage(page, preview, transitionDuration);
        }
    }

    private DesktopPagePreview? Acquire() => availablePages.Count > 0 ? availablePages.Pop() : null;

    private void UpdatePage(int page, DesktopPagePreview preview, TimeSpan? transitionDuration)
    {
        double fullContentOffset = layoutCalculator.CalculateContentOffset(currentOffset, workspace.Width);
        double baseX = page * (workspace.Width + layoutCalculator.PageSpacing) - fullContentOffset;
        double targetX = layoutCalculator.CalculatePageX(page,
            workspace.Width,
            currentOffset,
            currentSpacingProgress);
        double viewportWidth = GetViewportWidth();
        double viewportHeight = GetViewportHeight();
        double pageScreenX = (viewportWidth / 2) + ((baseX - (viewportWidth / 2)) * overviewScale);
        double pageScreenY = (viewportHeight / 2) * (1 - overviewScale);
        double titleX = pageScreenX + ((workspace.Width * overviewScale) - preview.TitleEditor.Width) / 2;
        double translationX = targetX - baseX;

        Canvas.SetLeft(preview.PageHost, leadingSpace + baseX);
        Canvas.SetTop(preview.PageHost, 0);
        Canvas.SetLeft(preview.TitleEditor, titleX);
        Canvas.SetTop(preview.TitleEditor, pageScreenY - preview.TitleEditor.Height);
        preview.Update(translationX, translationX * overviewScale, transitionDuration);
    }

    private void ConfigureHost()
    {
        if (host is null || titleHost is null || overviewScale <= 0)
        {
            return;
        }

        double viewportWidth = workspace.Width / overviewScale;
        leadingSpace = Math.Max(0, (viewportWidth - workspace.Width) / 2);
        host.Width = viewportWidth;
        host.Height = workspace.Height;
        Canvas.SetLeft(host, -leadingSpace);
        titleHost.Width = GetViewportWidth();
        titleHost.Height = GetViewportHeight();
    }

    private void CreatePagePool(FrameworkElement scaleHost)
    {
        if (host is null || titleHost is null)
        {
            return;
        }

        int capacity = layoutCalculator.CalculateVisiblePageCapacity(overviewScale);
        Visual scaleVisual = ElementCompositionPreview.GetElementVisual(scaleHost);

        for (int index = 0; index < capacity; index++)
        {
            DesktopPagePreview page = new(scaleVisual,
                overviewScale,
                localizer.GetText("PageTitleEditButton"),
                localizer.GetText("PageTitleSaveButton"),
                localizer.GetText("PageTitleCancelButton"));
            page.Click += HandlePageClicked;
            page.TitleEditor.ViewModel.TitleSubmitted += HandleTitleSubmitted;
            page.Hide();
            pagePool.Add(page);
            availablePages.Push(page);
            host.Children.Add(page.PageHost);
            titleHost.Children.Add(page.TitleEditor);
        }
    }

    private double GetViewportWidth() =>
        scaleHost?.ActualWidth > 0 ? scaleHost.ActualWidth : workspace.Width;

    private double GetViewportHeight() =>
        scaleHost?.ActualHeight > 0 ? scaleHost.ActualHeight : workspace.Height;

    private void HandlePageClicked(object sender, RoutedEventArgs args)
    {
        if (started && sender is DesktopPagePreview page)
        {
            PageInvoked?.Invoke(page.Page);
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
            if (started && visiblePages.TryGetValue(page, out DesktopPagePreview? preview))
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
                    page.Bind(page.Page,
                        workspace.Width,
                        workspace.Height,
                        background,
                        pageTitleStore.GetTitle(page.Page));
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
