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
    DesktopPageLayoutCalculator layoutCalculator,
    DesktopBackgroundBrushFactory backgroundBrushFactory,
    ILogger<DesktopPageStrip> logger) :
    IDisposable
{
    private readonly Dictionary<int, DesktopPagePreview> visiblePages = [];
    private readonly List<DesktopPagePreview> pagePool = [];
    private readonly Stack<DesktopPagePreview> availablePages = [];
    private Canvas? host;
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

    public void Start(Canvas canvas, FrameworkElement scaleHost, double scale)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (started)
        {
            return;
        }

        started = true;
        host = canvas;
        overviewScale = scale;
        interactionEnabled = false;
        ConfigureHost();
        CreatePagePool(scaleHost);
        backgroundSource.BackgroundChanged += HandleBackgroundChanged;
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

        foreach (DesktopPagePreview page in pagePool)
        {
            page.Click -= HandlePageClicked;
            page.Reset();
            page.Dispose();
        }

        visiblePages.Clear();
        availablePages.Clear();
        pagePool.Clear();
        host?.Children.Clear();
        host = null;
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

                preview.Bind(page, workspace.Width, workspace.Height, background);
                visiblePages.Add(page, preview);
            }
            else
            {
                preview.Bind(page, workspace.Width, workspace.Height, background);
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
        Canvas.SetLeft(preview, leadingSpace + baseX);
        Canvas.SetTop(preview, 0);
        Canvas.SetLeft(preview.ShadowHost, leadingSpace + baseX);
        Canvas.SetTop(preview.ShadowHost, 0);
        preview.Update(targetX - baseX, transitionDuration);
    }

    private void ConfigureHost()
    {
        if (host is null || overviewScale <= 0)
        {
            return;
        }

        double viewportWidth = workspace.Width / overviewScale;
        leadingSpace = Math.Max(0, (viewportWidth - workspace.Width) / 2);
        host.Width = viewportWidth;
        host.Height = workspace.Height;
        Canvas.SetLeft(host, -leadingSpace);
    }

    private void CreatePagePool(FrameworkElement scaleHost)
    {
        if (host is null)
        {
            return;
        }

        int capacity = layoutCalculator.CalculateVisiblePageCapacity(overviewScale);
        Visual scaleVisual = ElementCompositionPreview.GetElementVisual(scaleHost);

        for (int index = 0; index < capacity; index++)
        {
            DesktopPagePreview page = new(scaleVisual, overviewScale);
            page.Click += HandlePageClicked;
            page.Hide();
            pagePool.Add(page);
            availablePages.Push(page);
            Canvas.SetZIndex(page.ShadowHost, 0);
            Canvas.SetZIndex(page, 1);
            host.Children.Add(page.ShadowHost);
            host.Children.Add(page);
        }
    }

    private void HandlePageClicked(object sender, RoutedEventArgs args)
    {
        if (started && sender is DesktopPagePreview page)
        {
            PageInvoked?.Invoke(page.Page);
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
                    page.Bind(page.Page, workspace.Width, workspace.Height, background);
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
