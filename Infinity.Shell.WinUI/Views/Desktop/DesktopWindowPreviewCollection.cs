using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowPreviewCollection(DesktopWindowPreviewFactory factory, IWindowGeometryReader geometryReader, IWindowStack windowStack, ITrackedForegroundWindowTarget trackedForegroundWindowTarget, DesktopWindowSelectionModel selection, DesktopWindowGroupDragCoordinator groupDragCoordinator, DesktopWindowGroupStackAnimator groupStackAnimator, DesktopWindowDropNavigationCoordinator dropNavigationCoordinator, DesktopWindowPlacementCoordinator placementCoordinator) :
    IDisposable
{
    private readonly Dictionary<nint, DesktopWindowPreview> previews = [];
    private Canvas? backgroundHost;
    private Canvas? host;
    private Canvas? focusHost;
    private nint pendingForegroundHandle;
    private string filterText = string.Empty;
    private bool interactionEnabled;
    private bool disposed;
    private bool placementEventsSubscribed;
    private DesktopCaptureViewport captureViewport;

    public void SetCaptureViewport(DesktopCaptureViewport viewport)
    {
        if (captureViewport == viewport) return;
        captureViewport = viewport;
        foreach (DesktopWindowPreview preview in previews.Values)
        {
            preview.SetCaptureViewport(viewport);
        }
    }

    public event Action<nint>? WindowInvoked;

    public event Action<nint>? WindowPositionChanged;

    public event Action<nint, double, double>? WindowDragMoved;

    public event Action<nint>? WindowDragCompleted;

    public IReadOnlyList<TrackedWindow> Synchronise(Canvas backgroundCanvas, Canvas canvas, Canvas focusCanvas, IEnumerable<TrackedWindow> trackedWindows, double layoutScale)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!placementEventsSubscribed)
        {
            placementCoordinator.PlacementStarting += HandlePlacementStarting;
            placementCoordinator.PlacementCompleted += HandlePlacementCompleted;
            placementEventsSubscribed = true;
        }

        backgroundHost = backgroundCanvas;
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
                preview = factory.Create(backgroundCanvas, canvas, focusCanvas, trackedWindow.Handle, layoutScale);
                preview.SetCaptureViewport(captureViewport);
                preview.Invoked += HandleWindowInvoked;
                preview.SelectionToggled += HandleWindowSelectionToggled;
                preview.PositionChanged += HandleWindowPositionChanged;
                preview.DragMoved += HandleWindowDragMoved;
                preview.DragStarted += HandleWindowDragStarted;
                preview.DragCompleted += HandleWindowDragCompleted;
                preview.SetInteractionEnabled(interactionEnabled);
                previews.Add(trackedWindow.Handle, preview);
            }

            preview.RefreshSourceGeometry(trackedWindow, geometryReader);
            preview.SetFilterMatch(WindowTitleFilter.Matches(trackedWindow.Title, filterText));
            preview.SetKeyboardFocused(trackedWindow.Handle == selection.FocusedHandle);
            preview.SetSelected(selection.SelectedHandles.Contains(trackedWindow.Handle));
            preview.SetZIndex(zIndex);
        }

        EnsureSelection(orderedWindows);

        return orderedWindows;
    }

    internal bool TryGet(nint handle, out DesktopWindowPreview? preview) => previews.TryGetValue(handle, out preview);

    public nint SetFilter(string value, IEnumerable<TrackedWindow> trackedWindows)
    {
        if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(filterText))
        {
            ClearSelection();
        }

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

        if (matches.Any(window => window.Handle == selection.FocusedHandle))
        {
            return selection.FocusedHandle;
        }

        return SetFocused(matches.FirstOrDefault()?.Handle ?? 0);
    }

    public nint SelectNext(bool forward, IEnumerable<TrackedWindow> trackedWindows)
    {
        TrackedWindow[] matches = GetOrderedMatches(trackedWindows);

        if (matches.Length == 0)
        {
            return SetFocused(0);
        }

        int currentIndex = Array.FindIndex(matches, window => window.Handle == selection.FocusedHandle);
        int nextIndex = forward
            ? currentIndex >= 0 && currentIndex < matches.Length - 1 ? currentIndex + 1 : 0
            : currentIndex > 0 ? currentIndex - 1 : matches.Length - 1;
        return SetFocused(matches[nextIndex].Handle);
    }

    public void Refresh(TrackedWindow trackedWindow)
    {
        if (previews.TryGetValue(trackedWindow.Handle, out DesktopWindowPreview? preview))
        {
            preview.RefreshSourceGeometry(trackedWindow, geometryReader);
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
        if (placementEventsSubscribed)
        {
            placementCoordinator.PlacementStarting -= HandlePlacementStarting;
            placementCoordinator.PlacementCompleted -= HandlePlacementCompleted;
            placementEventsSubscribed = false;
        }
        foreach (DesktopWindowPreview preview in previews.Values)
        {
            preview.Invoked -= HandleWindowInvoked;
            preview.SelectionToggled -= HandleWindowSelectionToggled;
            preview.PositionChanged -= HandleWindowPositionChanged;
            preview.DragMoved -= HandleWindowDragMoved;
            preview.DragStarted -= HandleWindowDragStarted;
            preview.DragCompleted -= HandleWindowDragCompleted;
            preview.Dispose();
        }

        previews.Clear();

        backgroundHost?.Children.Clear();
        host?.Children.Clear();
        focusHost?.Children.Clear();

        backgroundHost = null;
        host = null;
        focusHost = null;
        filterText = string.Empty;
        pendingForegroundHandle = 0;
        groupDragCoordinator.Cancel();
        groupStackAnimator.Reset();
        selection.Clear();
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
        preview.SelectionToggled -= HandleWindowSelectionToggled;
        preview.PositionChanged -= HandleWindowPositionChanged;
        preview.DragMoved -= HandleWindowDragMoved;
        preview.DragStarted -= HandleWindowDragStarted;
        preview.DragCompleted -= HandleWindowDragCompleted;
        preview.Dispose();
        backgroundHost?.Children.Remove(preview.BackgroundHost);
        host?.Children.Remove(preview.Host);
        focusHost?.Children.Remove(preview.FocusHost);

        if (pendingForegroundHandle == handle)
        {
            pendingForegroundHandle = 0;
        }

        if (selection.FocusedHandle == handle)
        {
            selection.Focus(0);
        }

        if (selection.SelectedHandles.Contains(handle))
        {
            selection.RemoveSelected(handle);
        }

        if (groupStackAnimator.LeaderHandle == handle)
        {
            groupDragCoordinator.Cancel();
            groupStackAnimator.End(previews);
        }
        else
        {
            groupStackAnimator.Remove(handle);
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

    public void RefreshGroupStack()
    {
        if (groupStackAnimator.IsActive)
        {
            groupStackAnimator.Update(previews);
        }
    }

    public nint SelectFirst(IEnumerable<TrackedWindow> windows)
    {
        nint handle = windows.OrderBy(window => window.CanvasY).ThenBy(window => window.CanvasX).ThenBy(window => (long)window.Handle).FirstOrDefault()?.Handle ?? 0;
        return SetFocused(handle);
    }

    public nint SelectWithin(IEnumerable<TrackedWindow> windows, bool forward)
    {
        TrackedWindow[] ordered = [.. windows.OrderBy(window => window.CanvasY).ThenBy(window => window.CanvasX).ThenBy(window => (long)window.Handle)];

        if (ordered.Length == 0)
        {
            ClearSelection();
            return 0;
        }

        int currentIndex = Array.FindIndex(ordered, window => window.Handle == selection.FocusedHandle);
        int nextIndex = forward
            ? currentIndex >= 0 && currentIndex < ordered.Length - 1 ? currentIndex + 1 : 0
            : currentIndex > 0 ? currentIndex - 1 : ordered.Length - 1;
        nint handle = ordered[nextIndex].Handle;
        return SetFocused(handle);
    }

    public IReadOnlySet<nint> GetSelectedHandles() => selection.SelectedHandles;

    public nint GetFocusedHandle() => selection.FocusedHandle;

    public void ClearSelection()
    {
        ClearSelectionVisuals();
        SetFocused(0);
        selection.Clear();
    }

    public bool TryClearMultiSelection()
    {
        if (selection.SelectedHandles.Count == 0)
        {
            return false;
        }

        ClearSelectionVisuals();
        selection.ClearSelectedHandles();
        SetFocused(selection.FocusedHandle);
        return true;
    }

    private void HandleWindowInvoked(nint handle) => WindowInvoked?.Invoke(handle);

    private void HandleWindowSelectionToggled(nint handle)
    {
        SetFocused(handle);
        bool isSelected = selection.ToggleSelected(handle);

        if (previews.TryGetValue(handle, out DesktopWindowPreview? preview))
        {
            preview.SetSelected(isSelected);
        }
    }

    private void HandleWindowPositionChanged(nint handle) => WindowPositionChanged?.Invoke(handle);

    private void HandlePlacementStarting(IReadOnlyList<nint> handles)
    {
        if (!interactionEnabled) return;
        foreach (nint handle in handles)
        {
            if (previews.TryGetValue(handle, out DesktopWindowPreview? preview)) preview.BeginPlacementAnimation();
        }
    }

    private void HandlePlacementCompleted(IReadOnlyList<nint> handles)
    {
        foreach (nint handle in handles)
        {
            if (previews.TryGetValue(handle, out DesktopWindowPreview? preview)) preview.EndPlacementAnimation();
        }
        // Force one final geometry/layout update after the whole native batch.
        // Intermediate resize notifications must not consume the animation.
        foreach (nint handle in handles) WindowPositionChanged?.Invoke(handle);
    }

    private void HandleWindowDragMoved(nint handle, double pointerX, double pointerY)
    {
        if (handle == groupStackAnimator.LeaderHandle)
        {
            groupStackAnimator.Update(previews);
        }

        WindowDragMoved?.Invoke(handle, pointerX, pointerY);
    }

    private void HandleWindowDragStarted(nint handle)
    {
        PromoteWindowToForeground(handle);

        if (!selection.SelectedHandles.Contains(handle) || selection.SelectedHandles.Count < 2 || !groupDragCoordinator.Begin(handle, selection.SelectedHandles))
        {
            ClearSelection();
            return;
        }

        groupStackAnimator.Begin(handle, selection.SelectedHandles, previews);
    }

    private void HandleWindowDragCompleted(DesktopWindowDragCompletion completion)
    {
        bool moved = completion.WasMoved;

        if (completion.IsGroupDrag && completion.Handle == groupStackAnimator.LeaderHandle)
        {
            DesktopSnapPlacement? snapPlacement = completion.SnapTarget?.Placement;
            moved = groupDragCoordinator.Complete(completion.Handle, completion.HorizontalDelta, completion.VerticalDelta, snapPlacement);
            groupStackAnimator.End(previews);
            groupDragCoordinator.Cancel();
        }

        WindowDragCompleted?.Invoke(completion.Handle);

        if (moved)
        {
            dropNavigationCoordinator.NavigateToDroppedWindow(completion.Handle);
        }
    }

    private void PromoteWindowToForeground(nint handle)
    {
        if (!previews.ContainsKey(handle))
        {
            return;
        }

        pendingForegroundHandle = handle;
        trackedForegroundWindowTarget.SetTrackedForegroundWindow(handle);
        windowStack.BringToFront(handle);

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
            bool focusStillExists = trackedWindows.Any(window => window.Handle == selection.FocusedHandle);
            return SetFocused(focusStillExists ? selection.FocusedHandle : 0);
        }

        TrackedWindow[] matches = GetOrderedMatches(trackedWindows);
        nint handle = matches.Any(window => window.Handle == selection.FocusedHandle) ? selection.FocusedHandle : matches.FirstOrDefault()?.Handle ?? 0;

        return SetFocused(handle);
    }

    private TrackedWindow[] GetOrderedMatches(IEnumerable<TrackedWindow> trackedWindows) =>
        [.. trackedWindows
            .Where(window => WindowTitleFilter.Matches(window.Title, filterText))
            .OrderBy(window => window.CanvasX)
            .ThenBy(window => window.CanvasY)
            .ThenBy(window => (long)window.Handle)];

    private nint SetFocused(nint handle)
    {
        if (selection.FocusedHandle == handle)
        {
            if (previews.TryGetValue(handle, out DesktopWindowPreview? focused))
            {
                focused.SetKeyboardFocused(handle != 0);
            }

            return handle;
        }

        if (previews.TryGetValue(selection.FocusedHandle, out DesktopWindowPreview? previous))
        {
            previous.SetKeyboardFocused(false);
        }

        selection.Focus(handle);

        if (previews.TryGetValue(selection.FocusedHandle, out DesktopWindowPreview? current))
        {
            current.SetKeyboardFocused(true);
        }

        return handle;
    }

    private void ClearSelectionVisuals()
    {
        foreach (nint handle in selection.SelectedHandles)
        {
            if (previews.TryGetValue(handle, out DesktopWindowPreview? preview))
            {
                preview.SetSelected(false);
            }
        }
    }
}
