using System;
using Infinity.Platform.Windows;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowDragFrames
{
    private Action<DesktopWindowDragFrame>? updated;

    internal event Action<DesktopWindowDragFrame> Updated
    {
        add
        {
            if (updated is null)
            {
                CompositionTarget.Rendering += HandleRendering;
            }

            updated += value;
        }
        remove
        {
            updated -= value;
            if (updated is null)
            {
                CompositionTarget.Rendering -= HandleRendering;
            }
        }
    }

    internal Visual? Surface { get; private set; }

    internal DesktopWindowDragViewport Viewport { get; private set; } = new(0, 0, 1, 0, 0, 0, 0);

    internal void Configure(Visual surface, DesktopWindowDragViewport viewport)
    {
        Surface = surface;
        Viewport = viewport;
    }

    internal bool TryRead(out DesktopWindowDragFrame frame)
    {
        frame = default;
        if (!NativeDragPointer.TryGetPosition(out int x, out int y))
        {
            return false;
        }

        (double rootX, double rootY) = Viewport.FromScreen(x, y);
        frame = new(x, y, new(rootX, rootY), NativeDragPointer.IsButtonDown);
        return true;
    }

    private void HandleRendering(object? sender, object args)
    {
        if (TryRead(out DesktopWindowDragFrame frame))
        {
            updated?.Invoke(frame);
        }
    }
}

internal readonly record struct DesktopWindowDragFrame(int ScreenX, int ScreenY, Point Position, bool IsButtonDown);
