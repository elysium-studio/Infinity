using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public class StartupPageRestorerTests
{
    [Fact]
    public void RestoreReconstructsCurrentPageFromVisibleFrameBounds()
    {
        WindowStore store = new();
        TrackedWindow fullscreenWindow = CreateWindow(1, -3848, 1936);
        TrackedWindow secondPageWindow = CreateWindow(2, -1240, 800);
        store.Add(fullscreenWindow);
        store.Add(secondPageWindow);
        PanState state = new();
        TestGeometryReader geometryReader = new();
        geometryReader.SetVisibleGeometry(fullscreenWindow.Handle, -3840, 0, 1920, 1080);
        geometryReader.SetVisibleGeometry(secondPageWindow.Handle, -1240, 100, 800, 600);
        StartupPageRestorer restorer = CreateRestorer(store, state, geometryReader);

        restorer.Restore();

        Assert.Equal(3840, state.Offset);
        Assert.Equal(-8, fullscreenWindow.CanvasX);
        Assert.Equal(2600, secondPageWindow.CanvasX);
    }

    [Fact]
    public void RestoreKeepsPartiallyVisibleWindowsOnCurrentPage()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(1, -1810, 1830);
        store.Add(window);
        PanState state = new();
        TestGeometryReader geometryReader = new();
        geometryReader.SetVisibleGeometry(window.Handle, -1800, 100, 1820, 600);
        StartupPageRestorer restorer = CreateRestorer(store, state, geometryReader);

        restorer.Restore();

        Assert.Equal(0, state.Offset);
        Assert.Equal(-1810, window.CanvasX);
    }

    [Fact]
    public void RestoreUsesTrackedBoundsWhenVisibleFrameCannotBeRead()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(1, -1820, 800);
        store.Add(window);
        PanState state = new();
        StartupPageRestorer restorer = CreateRestorer(store, state, new TestGeometryReader());

        restorer.Restore();

        Assert.Equal(1920, state.Offset);
        Assert.Equal(100, window.CanvasX);
    }

    private static StartupPageRestorer CreateRestorer(WindowStore store,
        PanState state,
        IWindowGeometryReader geometryReader) =>
        new(store,
            state,
            new TestWorkspace(),
            geometryReader,
            NullLogger<StartupPageRestorer>.Instance);

    private static TrackedWindow CreateWindow(int handle, int canvasX, int width) => new()
    {
        Handle = new IntPtr(handle),
        CanvasX = canvasX,
        CanvasY = 100,
        Width = width,
        Height = 600
    };

    private class TestGeometryReader : IWindowGeometryReader
    {
        private readonly Dictionary<IntPtr, Geometry> visibleGeometries = [];

        public bool IsMinimised(IntPtr windowHandle) => false;

        public bool IsVisible(IntPtr windowHandle) => true;

        public void SetVisibleGeometry(IntPtr handle, int x, int y, int width, int height) =>
            visibleGeometries[handle] = new(x, y, width, height);

        public bool TryReadGeometry(IntPtr windowHandle, out int x, out int y, out int width, out int height) =>
            TryReadVisibleGeometry(windowHandle, out x, out y, out width, out height);

        public bool TryReadVisibleGeometry(IntPtr windowHandle, out int x, out int y, out int width, out int height)
        {
            if (visibleGeometries.TryGetValue(windowHandle, out Geometry geometry))
            {
                x = geometry.X;
                y = geometry.Y;
                width = geometry.Width;
                height = geometry.Height;
                return true;
            }

            x = 0;
            y = 0;
            width = 0;
            height = 0;
            return false;
        }

        private readonly record struct Geometry(int X, int Y, int Width, int Height);
    }

    private class TestWorkspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Height => 1080;

        public int Width => 1920;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public IntPtr GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }
    }
}
