using System.Drawing;
using System.Runtime.InteropServices;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class WindowBoundaryResizerTests
{
    [Fact]
    public void InvalidWindowsAreRejected()
    {
        WindowBoundaryResizer resizer = new();
        Assert.False(resizer.TryGetLimits(0, out _));
        Assert.False(resizer.TryResize(0, 0, 0, 800, 600));
    }

    [Fact]
    public async Task NativeWindowCanBeResizedAcrossThreadsWithoutActivation()
    {
        TaskCompletionSource<(nint Handle, uint ThreadId)> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread owner = new(() => RunWindow(ready)) { IsBackground = true, Name = "Shared boundary test window" };
        owner.Start();
        (nint handle, uint threadId) = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            WindowBoundaryResizer resizer = new();
            Assert.True(resizer.TryGetLimits(handle, out WindowResizeLimits limits));
            Assert.InRange(limits.MinimumWidth, 1, limits.MaximumWidth);
            Assert.InRange(limits.MinimumHeight, 1, limits.MaximumHeight);
            nint foreground = GetForegroundWindow();
            Assert.True(resizer.TryResize(handle, -30000, -30000, 800, 600));
            RectangleBounds bounds = default;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                Assert.True(GetWindowRect(handle, out bounds));
                if (bounds.Right - bounds.Left == 800 && bounds.Bottom - bounds.Top == 600)
                {
                    break;
                }

                await Task.Delay(10);
            }

            Assert.Equal(800, bounds.Right - bounds.Left);
            Assert.Equal(600, bounds.Bottom - bounds.Top);
            Assert.NotEqual(handle, GetForegroundWindow());
            Assert.NotEqual(handle, foreground);
        }
        finally
        {
            PostThreadMessageW(threadId, 0x0012, 0, 0);
            Assert.True(owner.Join(TimeSpan.FromSeconds(5)));
        }
    }

    private static void RunWindow(TaskCompletionSource<(nint Handle, uint ThreadId)> ready)
    {
        nint handle = CreateWindowExW(0x08000080, "STATIC", "Infinity shared boundary test", 0x10CF0000, -30000, -30000, 1000, 800, 0, 0, 0, 0);
        if (handle == 0)
        {
            ready.TrySetException(new InvalidOperationException($"Test window creation failed: {Marshal.GetLastPInvokeError()}"));
            return;
        }

        try
        {
            ready.TrySetResult((handle, GetCurrentThreadId()));
            while (GetMessageW(out Message message, 0, 0, 0) > 0)
            {
                TranslateMessage(in message);
                DispatchMessageW(in message);
            }
        }
        finally
        {
            DestroyWindow(handle);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint handle);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out Message message, nint handle, uint minimum, uint maximum);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(in Message message);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(in Message message);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint handle, out RectangleBounds bounds);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectangleBounds
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Handle;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public Point Position;
        public uint Private;
    }
}
