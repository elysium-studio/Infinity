using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowPreviewCollection(DesktopWindowPreviewFactory factory, IWindowGeometryReader geometryReader) :
    IDisposable
{
    private readonly Dictionary<nint, DesktopWindowPreview> previews = [];
    private Canvas? host;
    private Canvas? focusHost;
    private nint pendingForegroundHandle;
    private nint selectedHandle;
    private string filterText = string.Empty;
    private double thumbnailWorldScale = 1;
    private bool interactionEnabled;
    private bool disposed;

    public event Action<nint>? WindowInvoked;

    public event Action<nint>? WindowPositionChanged;

    public event Action<nint, double, double>? WindowDragMoved;

    public event Action<nint>? WindowDragCompleted;

    public IReadOnlyList<TrackedWindow> Synchronise(Canvas canvas, Canvas focusCanvas, IEnumerable<TrackedWindow> trackedWindows, double layoutScale)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        host = canvas;
        focusHost = focusCanvas;

        TrackedWindow[] orderedWindows = OrderWindows(trackedWindows);
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
                preview = factory.Create(canvas, focusCanvas, trackedWindow.Handle, layoutScale);
                preview.Invoked += HandleWindowInvoked;
                preview.PositionChanged += HandleWindowPositionChanged;
                preview.Foregrounded += HandleWindowForegrounded;
                preview.DragMoved += HandleWindowDragMoved;
                preview.DragCompleted += HandleWindowDragCompleted;
                preview.SetInteractionEnabled(interactionEnabled);
                previews.Add(trackedWindow.Handle, preview);
            }

            preview.RefreshSourceSize(trackedWindow, geometryReader);
            preview.SetThumbnailWorldScale(thumbnailWorldScale);
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

    public bool Activate(nint handle)
    {
        if (!previews.TryGetValue(handle, out DesktopWindowPreview? preview))
        {
            return false;
        }

        preview.Activate();
        return true;
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

    public void SetThumbnailWorldScale(double value)
    {
        thumbnailWorldScale = double.IsFinite(value) && value > 0 ? value : 1;

        foreach (DesktopWindowPreview preview in previews.Values)
        {
            preview.SetThumbnailWorldScale(thumbnailWorldScale);
        }
    }

    internal void SetSnapTarget(nint handle, DesktopWindowSnapTarget? target)
    {
        if (previews.TryGetValue(handle, out DesktopWindowPreview? preview))
        {
            preview.SetSnapTarget(target);
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
            preview.Foregrounded -= HandleWindowForegrounded;
            preview.DragMoved -= HandleWindowDragMoved;
            preview.DragCompleted -= HandleWindowDragCompleted;
            preview.Dispose();
        }

        previews.Clear();

        host?.Children.Clear();
        focusHost?.Children.Clear();

        host = null;
        focusHost = null;
        filterText = string.Empty;
        selectedHandle = 0;
        pendingForegroundHandle = 0;
        thumbnailWorldScale = 1;
        interactionEnabled = false;
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
        preview.Foregrounded -= HandleWindowForegrounded;
        preview.DragMoved -= HandleWindowDragMoved;
        preview.DragCompleted -= HandleWindowDragCompleted;
        preview.Dispose();
        host?.Children.Remove(preview.Host);
        focusHost?.Children.Remove(preview.FocusHost);

        if (selectedHandle == handle)
        {
            selectedHandle = 0;
        }

        if (pendingForegroundHandle == handle)
        {
            pendingForegroundHandle = 0;
        }
    }

    public void SetPageReorderState(DesktopPageReorderPreviewState? state, IEnumerable<TrackedWindow> trackedWindows, double workspaceWidth)
    {
        foreach (TrackedWindow window in trackedWindows)
        {
            if (previews.TryGetValue(window.Handle, out DesktopWindowPreview? preview))
            {
                preview.SetPagePromoted(state is not null && PageReorderMapping.GetPage(window, workspaceWidth) == state.SourcePage);
            }
        }
    }

    public void RefreshSelection(IEnumerable<TrackedWindow> trackedWindows) => EnsureSelection(trackedWindows);

    private void HandleWindowInvoked(nint handle) => WindowInvoked?.Invoke(handle);

    private void HandleWindowPositionChanged(nint handle) => WindowPositionChanged?.Invoke(handle);

    private void HandleWindowDragMoved(nint handle, double pointerX, double pointerY) => WindowDragMoved?.Invoke(handle, pointerX, pointerY);

    private void HandleWindowDragCompleted(nint handle) => WindowDragCompleted?.Invoke(handle);

    private void HandleWindowForegrounded(nint handle)
    {
        if (!previews.ContainsKey(handle))
        {
            return;
        }

        pendingForegroundHandle = handle;

        DesktopWindowPreview[] orderedPreviews = [.. previews
            .Where(item => item.Key != handle)
            .OrderBy(item => item.Value.ZIndex)
            .ThenBy(item => (long)item.Key)
            .Select(item => item.Value)];

        for (int zIndex = 0; zIndex < orderedPreviews.Length; zIndex++)
        {
            orderedPreviews[zIndex].SetZIndex(zIndex);
        }

        previews[handle].SetZIndex(orderedPreviews.Length);
    }

    private TrackedWindow[] OrderWindows(IEnumerable<TrackedWindow> trackedWindows)
    {
        TrackedWindow[] orderedWindows = [.. trackedWindows
            .OrderByDescending(window => window.ZIndex)
            .ThenBy(window => (long)window.Handle)];

        if (pendingForegroundHandle == 0)
        {
            return orderedWindows;
        }

        int foregroundIndex = Array.FindIndex(orderedWindows, window => window.Handle == pendingForegroundHandle);

        if (foregroundIndex < 0)
        {
            pendingForegroundHandle = 0;
            return orderedWindows;
        }

        TrackedWindow foregroundWindow = orderedWindows[foregroundIndex];

        for (int index = foregroundIndex; index < orderedWindows.Length - 1; index++)
        {
            orderedWindows[index] = orderedWindows[index + 1];
        }

        orderedWindows[^1] = foregroundWindow;

        if (orderedWindows.All(window => window.Handle == pendingForegroundHandle || foregroundWindow.ZIndex <= window.ZIndex))
        {
            pendingForegroundHandle = 0;
        }

        return orderedWindows;
    }

    private nint EnsureSelection(IEnumerable<TrackedWindow> trackedWindows)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return SetSelected(0);
        }

        TrackedWindow[] matches = GetOrderedMatches(trackedWindows);
        nint handle = matches.Any(window => window.Handle == selectedHandle) ? selectedHandle : matches.FirstOrDefault()?.Handle ?? 0;

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
