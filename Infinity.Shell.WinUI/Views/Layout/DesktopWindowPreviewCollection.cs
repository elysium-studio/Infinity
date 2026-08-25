using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowPreviewCollection(DesktopWindowPreviewFactory factory,
    IWindowGeometryReader geometryReader) :
    IDisposable
{
    private readonly Dictionary<nint, DesktopWindowPreview> previews = [];
    private Canvas? host;
    private nint promotedWindowHandle;
    private string filterText = string.Empty;
    private bool interactionEnabled;
    private bool disposed;

    public event Action<nint>? WindowInvoked;

    public event Action<nint>? WindowPositionChanged;

    public IReadOnlyList<TrackedWindow> Synchronise(Canvas canvas,
        IEnumerable<TrackedWindow> trackedWindows,
        double layoutScale)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        host = canvas;
        TrackedWindow[] orderedWindows = [.. trackedWindows
            .OrderByDescending(window => window.ZIndex)
            .ThenBy(window => (long)window.Handle)];
        HashSet<nint> currentHandles = [.. orderedWindows.Select(window => window.Handle)];

        foreach (nint handle in previews.Keys.Where(handle => !currentHandles.Contains(handle)).ToArray())
        {
            Remove(handle);
        }

        for (int zIndex = 0; zIndex < orderedWindows.Length; zIndex++)
        {
            TrackedWindow trackedWindow = orderedWindows[zIndex];

            if (!previews.TryGetValue(trackedWindow.Handle, out DesktopWindowPreview? preview))
            {
                preview = factory.Create(canvas, trackedWindow.Handle, layoutScale);
                preview.Invoked += HandleWindowInvoked;
                preview.PositionChanged += HandleWindowPositionChanged;
                preview.Promoted += HandleWindowPromoted;
                preview.PromotionReleased += HandleWindowPromotionReleased;
                preview.SetInteractionEnabled(interactionEnabled);
                previews.Add(trackedWindow.Handle, preview);
            }

            preview.RefreshSourceSize(trackedWindow, geometryReader);
            preview.SetFilterMatch(WindowTitleFilter.Matches(trackedWindow.Title, filterText));
            preview.SetZIndex(zIndex);
        }

        return orderedWindows;
    }

    internal bool TryGet(nint handle, out DesktopWindowPreview? preview) => previews.TryGetValue(handle, out preview);

    public void SetFilter(string value, IEnumerable<TrackedWindow> trackedWindows)
    {
        filterText = value;

        foreach (TrackedWindow trackedWindow in trackedWindows)
        {
            if (previews.TryGetValue(trackedWindow.Handle, out DesktopWindowPreview? preview))
            {
                preview.SetFilterMatch(WindowTitleFilter.Matches(trackedWindow.Title, filterText));
            }
        }
    }

    public nint GetFirstMatchingWindow(IEnumerable<TrackedWindow> trackedWindows)
    {
        return trackedWindows
            .Where(window => WindowTitleFilter.Matches(window.Title, filterText))
            .OrderBy(window => window.CanvasX)
            .ThenBy(window => window.CanvasY)
            .Select(window => window.Handle)
            .FirstOrDefault();
    }

    public void Refresh(TrackedWindow trackedWindow)
    {
        if (previews.TryGetValue(trackedWindow.Handle, out DesktopWindowPreview? preview))
        {
            preview.RefreshSourceSize(trackedWindow, geometryReader);
            preview.SetFilterMatch(WindowTitleFilter.Matches(trackedWindow.Title, filterText));
        }
    }

    public void ClearTranslationTransitions()
    {
        foreach (DesktopWindowPreview preview in previews.Values)
        {
            preview.ClearTranslationTransition();
        }
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;

        foreach (DesktopWindowPreview preview in previews.Values)
        {
            preview.SetInteractionEnabled(value);
        }
    }

    public void Clear()
    {
        foreach (DesktopWindowPreview preview in previews.Values)
        {
            preview.Invoked -= HandleWindowInvoked;
            preview.PositionChanged -= HandleWindowPositionChanged;
            preview.Promoted -= HandleWindowPromoted;
            preview.PromotionReleased -= HandleWindowPromotionReleased;
            preview.Dispose();
        }

        previews.Clear();
        host?.Children.Clear();
        host = null;
        filterText = string.Empty;
        interactionEnabled = false;
        promotedWindowHandle = 0;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }

    private void Remove(nint handle)
    {
        if (!previews.Remove(handle, out DesktopWindowPreview? preview))
        {
            return;
        }

        preview.Invoked -= HandleWindowInvoked;
        preview.PositionChanged -= HandleWindowPositionChanged;
        preview.Promoted -= HandleWindowPromoted;
        preview.PromotionReleased -= HandleWindowPromotionReleased;
        preview.Dispose();
        host?.Children.Remove(preview.Host);

        if (promotedWindowHandle == handle)
        {
            promotedWindowHandle = 0;
        }
    }

    private void HandleWindowInvoked(nint handle) => WindowInvoked?.Invoke(handle);

    private void HandleWindowPositionChanged(nint handle) => WindowPositionChanged?.Invoke(handle);

    private void HandleWindowPromoted(nint handle)
    {
        nint previousHandle = promotedWindowHandle;
        promotedWindowHandle = handle;

        if (previousHandle != 0 && previousHandle != handle &&
            previews.TryGetValue(previousHandle, out DesktopWindowPreview? previousPreview))
        {
            previousPreview.SetPromoted(false);
        }

        if (previews.TryGetValue(handle, out DesktopWindowPreview? preview))
        {
            preview.SetPromoted(true);
        }
    }

    private void HandleWindowPromotionReleased(nint handle)
    {
        if (promotedWindowHandle == handle)
        {
            promotedWindowHandle = 0;
        }
    }
}
