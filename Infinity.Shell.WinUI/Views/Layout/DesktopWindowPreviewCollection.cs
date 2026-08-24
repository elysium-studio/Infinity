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
    private bool interactionEnabled;
    private bool disposed;

    public event Action<nint>? WindowInvoked;

    public IReadOnlyList<TrackedWindow> Synchronise(Canvas canvas, IEnumerable<TrackedWindow> trackedWindows)
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
                preview = factory.Create(canvas, trackedWindow.Handle);
                preview.Invoked += HandleWindowInvoked;
                preview.SetInteractionEnabled(interactionEnabled);
                previews.Add(trackedWindow.Handle, preview);
            }

            preview.RefreshSourceSize(trackedWindow, geometryReader);
            preview.SetZIndex(zIndex);
        }

        return orderedWindows;
    }

    internal bool TryGet(nint handle, out DesktopWindowPreview? preview) => previews.TryGetValue(handle, out preview);

    public void RefreshSourceSize(TrackedWindow trackedWindow)
    {
        if (previews.TryGetValue(trackedWindow.Handle, out DesktopWindowPreview? preview))
        {
            preview.RefreshSourceSize(trackedWindow, geometryReader);
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
            preview.Dispose();
        }

        previews.Clear();
        host?.Children.Clear();
        host = null;
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
        preview.Dispose();
        host?.Children.Remove(preview.Host);
    }

    private void HandleWindowInvoked(nint handle) => WindowInvoked?.Invoke(handle);
}
