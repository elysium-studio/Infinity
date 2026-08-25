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
    private nint selectedHandle;
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

        EnsureSelection(orderedWindows);

        return orderedWindows;
    }

    internal bool TryGet(nint handle, out DesktopWindowPreview? preview) => previews.TryGetValue(handle, out preview);

    public nint SetFilter(string value, IEnumerable<TrackedWindow> trackedWindows)
    {
        filterText = value;
        TrackedWindow[] windows = [.. trackedWindows];

        foreach (TrackedWindow trackedWindow in windows)
        {
            if (previews.TryGetValue(trackedWindow.Handle, out DesktopWindowPreview? preview))
            {
                preview.SetFilterMatch(WindowTitleFilter.Matches(trackedWindow.Title, filterText));
            }
        }

        return EnsureSelection(windows);
    }

    public nint GetSelectedMatchingWindow(IEnumerable<TrackedWindow> trackedWindows)
    {
        TrackedWindow[] matches = GetOrderedMatches(trackedWindows);

        if (matches.Any(window => window.Handle == selectedHandle))
        {
            return selectedHandle;
        }

        return SetSelected(matches.FirstOrDefault()?.Handle ?? 0);
    }

    public nint SelectNext(bool forward, IEnumerable<TrackedWindow> trackedWindows)
    {
        TrackedWindow[] matches = GetOrderedMatches(trackedWindows);

        if (matches.Length == 0)
        {
            return SetSelected(0);
        }

        int currentIndex = Array.FindIndex(matches, window => window.Handle == selectedHandle);
        int nextIndex = forward
            ? currentIndex >= 0 && currentIndex < matches.Length - 1 ? currentIndex + 1 : 0
            : currentIndex > 0 ? currentIndex - 1 : matches.Length - 1;
        return SetSelected(matches[nextIndex].Handle);
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
        selectedHandle = 0;
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

        if (selectedHandle == handle)
        {
            selectedHandle = 0;
        }
    }

    public void RefreshSelection(IEnumerable<TrackedWindow> trackedWindows) =>
        EnsureSelection(trackedWindows);

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

    private nint EnsureSelection(IEnumerable<TrackedWindow> trackedWindows)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return SetSelected(0);
        }

        TrackedWindow[] matches = GetOrderedMatches(trackedWindows);
        nint handle = matches.Any(window => window.Handle == selectedHandle)
            ? selectedHandle
            : matches.FirstOrDefault()?.Handle ?? 0;
        return SetSelected(handle);
    }

    private TrackedWindow[] GetOrderedMatches(IEnumerable<TrackedWindow> trackedWindows) =>
        [.. trackedWindows
            .Where(window => WindowTitleFilter.Matches(window.Title, filterText))
            .OrderBy(window => window.CanvasX)
            .ThenBy(window => window.CanvasY)
            .ThenBy(window => (long)window.Handle)];

    private nint SetSelected(nint handle)
    {
        if (selectedHandle == handle)
        {
            if (previews.TryGetValue(handle, out DesktopWindowPreview? selected))
            {
                selected.SetSelected(handle != 0);
            }

            return handle;
        }

        if (previews.TryGetValue(selectedHandle, out DesktopWindowPreview? previous))
        {
            previous.SetSelected(false);
        }

        selectedHandle = handle;

        if (previews.TryGetValue(selectedHandle, out DesktopWindowPreview? current))
        {
            current.SetSelected(true);
        }

        return handle;
    }
}
