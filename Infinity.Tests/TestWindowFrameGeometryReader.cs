using Infinity.Platform.Abstractions;

namespace Infinity.Tests;

internal sealed class TestWindowFrameGeometryReader : IWindowGeometryReader
{
    public Dictionary<nint, (int Left, int Top, int Right, int Bottom)> Insets { get; } = [];

    public bool IsVisible(nint handle) => true;

    public bool IsMinimised(nint handle) => false;

    public bool TryReadGeometry(nint handle, out int x, out int y, out int width, out int height)
    {
        x = 100;
        y = 50;
        width = 1000;
        height = 800;
        return Insets.ContainsKey(handle);
    }


    public bool TryReadVisibleGeometry(nint handle, out int x, out int y, out int width, out int height)
    {
        Insets.TryGetValue(handle, out (int Left, int Top, int Right, int Bottom) inset);
        x = 100 + inset.Left;
        y = 50 + inset.Top;
        width = 1000 - inset.Left - inset.Right;
        height = 800 - inset.Top - inset.Bottom;
        return Insets.ContainsKey(handle);
    }
}
