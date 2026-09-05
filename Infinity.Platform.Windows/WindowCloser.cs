using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public sealed class WindowCloser(ILogger<WindowCloser> logger) : IWindowCloser, IDisposable
{
    private const uint WmClose = 0x0010;
    private const uint WmSysCommand = 0x0112;
    private const uint ScClose = 0xF060;
    private const string CloseMarker = "Elysium.Infinity.PendingFrameClose";
    private const SET_WINDOW_POS_FLAGS VisibilityFlags = SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS;
    private readonly ConcurrentDictionary<nint, int> pending = new();
    private readonly Lock gate = new();
    private int nextToken;
    private bool disposed;

    public bool TryClose(nint windowHandle)
    {
        HWND hwnd = new(windowHandle);
        if (windowHandle == 0 || !PInvoke.IsWindow(hwnd))
        {
            return false;
        }

        if (!IsApplicationFrame(hwnd) || !PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd))
        {
            return PInvoke.PostMessage(hwnd, WmClose, default, default);
        }

        lock (gate)
        {
            if (disposed)
            {
                return false;
            }

            if (pending.ContainsKey(windowHandle))
            {
                return true;
            }

            int token = unchecked(++nextToken);
            if (token == 0)
            {
                token = unchecked(++nextToken);
            }

            if (!SetMarker(hwnd, token))
            {
                logger.LogWarning("Cannot prepare packaged-window close. Handle={WindowHandle}", windowHandle);
                return false;
            }

            pending[windowHandle] = token;
            _ = Task.Run(() => CloseFrameAsync(hwnd, token));
            return true;
        }
    }


    private async Task CloseFrameAsync(HWND hwnd, int token)
    {
        try
        {
            lock (gate)
            {
                if (disposed || !OwnsMarker(hwnd, token))
                {
                    return;
                }

                if (!SetVisibility(hwnd, false))
                {
                    logger.LogWarning("Cannot hide packaged window before close. Handle={WindowHandle}", (nint)hwnd);
                    return;
                }
            }

            Stopwatch wait = Stopwatch.StartNew();
            while (OwnsMarker(hwnd, token) && PInvoke.IsWindowVisible(hwnd) && wait.ElapsedMilliseconds < 500)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }

            if (!OwnsMarker(hwnd, token))
            {
                return;
            }

            if (PInvoke.IsWindowVisible(hwnd))
            {
                logger.LogWarning("Packaged window did not acknowledge hide; close aborted. Handle={WindowHandle}", (nint)hwnd);
                return;
            }

            HRESULT flushResult = await Task.Run(() => PInvoke.DwmFlush()).WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            Marshal.ThrowExceptionForHR(flushResult.Value);
            lock (gate)
            {
                if (disposed || !OwnsMarker(hwnd, token))
                {
                    return;
                }

                if (!PInvoke.PostMessage(hwnd, WmSysCommand, new WPARAM(ScClose), default))
                {
                    logger.LogWarning("Packaged-window close request failed. Handle={WindowHandle}", (nint)hwnd);
                    return;
                }
            }

            wait.Restart();
            while (OwnsMarker(hwnd, token) && wait.ElapsedMilliseconds < 750)
            {
                await Task.Delay(25).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Packaged-window close did not complete. Handle={WindowHandle}", (nint)hwnd);
        }
        finally
        {
            lock (gate)
            {
                RestoreFrame(hwnd, token);
                pending.TryRemove((nint)hwnd, out _);
            }
        }
    }


    private static bool SetVisibility(HWND hwnd, bool visible) => PInvoke.SetWindowPos(hwnd, default, 0, 0, 0, 0, VisibilityFlags | (visible ? SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW : SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW));

    private static unsafe bool IsApplicationFrame(HWND hwnd)
    {
        Span<char> name = stackalloc char[256];
        fixed (char* pointer = name)
        {
            int length = PInvoke.GetClassName(hwnd, pointer, name.Length);
            return name[..Math.Max(0, length)].SequenceEqual("ApplicationFrameWindow");
        }
    }


    private static unsafe bool SetMarker(HWND hwnd, int token)
    {
        fixed (char* name = CloseMarker)
            return PInvoke.SetProp(hwnd, name, new HANDLE(token));
    }


    private static unsafe bool OwnsMarker(HWND hwnd, int token)
    {
        fixed (char* name = CloseMarker)
            return PInvoke.GetProp(hwnd, name) == new HANDLE(token);
    }


    private unsafe void RestoreFrame(HWND hwnd, int token)
    {
        if (!OwnsMarker(hwnd, token))
        {
            return;
        }

        if (!SetVisibility(hwnd, true))
        {
            logger.LogError("Could not restore packaged window after close. Handle={WindowHandle}, Error={Error}", (nint)hwnd, Marshal.GetLastPInvokeError());
            _ = PInvoke.ShowWindowAsync(hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }

        fixed (char* name = CloseMarker)
            PInvoke.RemoveProp(hwnd, name);
    }


    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            foreach ((nint hwnd, int token)in pending)
                RestoreFrame(new HWND(hwnd), token);
            pending.Clear();
        }
    }
}
