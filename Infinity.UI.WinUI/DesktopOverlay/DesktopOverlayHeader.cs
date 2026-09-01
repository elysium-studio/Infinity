using Elysium.Platform.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using WindowExtensions = Infinity.UI.WinUI.DesktopOverlayWindowStyle;
using WindowStyle = Infinity.UI.WinUI.DesktopOverlayExtendedWindowStyle;

namespace Infinity.UI.WinUI;

internal class DesktopOverlayHeader
{
    private const SET_WINDOW_POS_FLAGS SwpHideWindow = SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW;
    private const SET_WINDOW_POS_FLAGS SwpNoActivate = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
    private const SET_WINDOW_POS_FLAGS SwpNoMove = SET_WINDOW_POS_FLAGS.SWP_NOMOVE;
    private const SET_WINDOW_POS_FLAGS SwpNoSize = SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
    private const SET_WINDOW_POS_FLAGS SwpShowWindow = SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;

    private static readonly HWND HwndTopmost = new(new nint(-1));

    private readonly Dictionary<nint, DispatcherQueueTimer> activeTimers = [];
    private readonly ContentPresenter contentPresenter;
    private readonly HWND handle;
    private readonly Window window;
    private bool isVisible;
    private bool disposed;
    private DesktopOverlayHeaderPlacement placement = DesktopOverlayHeaderPlacement.Top;
    private HMONITOR currentMonitor;

    public DesktopOverlayHeader()
    {
        contentPresenter = new ContentPresenter
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        window = new Window
        {
            ExtendsContentIntoTitleBar = true,
            Content = contentPresenter,
            SystemBackdrop = new TransparentTintBackdrop()
        };

        window.SetTitleBar(null);
        OverlappedPresenter presenter = window.AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        presenter.IsResizable = false;

        handle = new HWND(WindowNative.GetWindowHandle(window));

        WindowStyle exStyle = (WindowStyle)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            (int)(exStyle | WindowStyle.Layered | WindowStyle.NoActivate | WindowStyle.ToolWindow));

        PInvoke.SetLayeredWindowAttributes(handle, new COLORREF(0), 0, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);

        WindowExtensions.SetBorderless(handle, true);
        WindowExtensions.SetSharpCorners(handle);
        WindowExtensions.SetTopMost(handle, true);

        DesktopOverlayHost.RegisterExcludedHandle(handle);

        PInvoke.SetWindowPos(handle, HwndTopmost, -32000, -32000, 1, 1,
            SwpNoActivate | SwpHideWindow);
    }

    public void Close()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        StopAnimation(handle);
        DesktopOverlayHost.UnregisterExcludedHandle(handle);
        window.Close();
    }

    public void Hide()
    {
        isVisible = false;

        window.DispatcherQueue.TryEnqueue(() =>
        {
            WindowStyle exStyle = (WindowStyle)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                (int)(exStyle | WindowStyle.NoActivate));

            StopAnimation(handle);
            Animate(window.DispatcherQueue, handle, from: 1f, to: 0f, durationMs: 200, completed: () =>
            {
                PInvoke.SetWindowPos(handle, HwndTopmost, -32000, -32000, 1, 1,
                    SwpNoActivate | SwpHideWindow);
            });
        });
    }

    public void SetContent(object? content)
    {
        window.DispatcherQueue.TryEnqueue(() =>
        {
            contentPresenter.Content = content;

            if (isVisible)
            {
                RepositionToWorkArea();
            }
        });
    }

    public void SetPlacement(DesktopOverlayHeaderPlacement value)
    {
        placement = value;

        window.DispatcherQueue.TryEnqueue(() =>
        {
            if (isVisible)
            {
                RepositionToWorkArea();
            }
        });
    }

    public void Show(MonitorHandle monitor)
    {
        currentMonitor = new HMONITOR(monitor.Value);
        isVisible = true;

        window.DispatcherQueue.TryEnqueue(() =>
        {
            RepositionToWorkArea();
            StopAnimation(handle);
            PInvoke.SetLayeredWindowAttributes(handle, new COLORREF(0), 0, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);

            PInvoke.SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0,
                SwpNoActivate | SwpNoSize | SwpNoMove | SwpShowWindow);

            Animate(window.DispatcherQueue, handle, from: 0f, to: 1f, durationMs: 300, completed: null);

            window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, AllowActivation);
        });
    }

    public void PromoteTopMost()
    {
        if (!isVisible)
        {
            return;
        }

        window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
        {
            if (isVisible)
            {
                PInvoke.SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0,
                    SwpNoActivate | SwpNoSize | SwpNoMove);
            }
        });
    }

    private static float EaseInOut(float progress) =>
        progress < 0.5f
            ? 2f * progress * progress
            : 1f - MathF.Pow(-2f * progress + 2f, 2f) / 2f;

    private static unsafe RectInt32 GetPrimaryMonitorWorkArea()
    {
        RectInt32 result = new(0, 0, 1920, 40);

        PInvoke.EnumDisplayMonitors(HDC.Null, null, (HMONITOR monitor, HDC deviceContext, RECT* rect, LPARAM data) =>
        {
            MONITORINFO info = new()
            {
                cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
            };

            PInvoke.GetMonitorInfo(monitor, ref info);

            if ((info.dwFlags & 0x1u) != 0)
            {
                result = new RectInt32(
                    info.rcWork.left,
                    info.rcWork.top,
                    info.rcWork.right - info.rcWork.left,
                    info.rcWork.bottom - info.rcWork.top);

                return false;
            }

            return true;
        }, new LPARAM(0));

        return result;
    }

    private static RectInt32 GetMonitorWorkArea(HMONITOR monitor)
    {
        if ((nint)monitor == 0)
        {
            return GetPrimaryMonitorWorkArea();
        }

        MONITORINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };

        if (!PInvoke.GetMonitorInfo(monitor, ref info))
        {
            return GetPrimaryMonitorWorkArea();
        }

        return new RectInt32(
            info.rcWork.left,
            info.rcWork.top,
            info.rcWork.right - info.rcWork.left,
            info.rcWork.bottom - info.rcWork.top);
    }

    private void Animate(DispatcherQueue dispatcherQueue, HWND windowHandle, float from, float to, int durationMs, Action? completed)
    {
        int totalSteps = Math.Max(1, durationMs / 16);
        int step = 0;

        DispatcherQueueTimer timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(16);
        timer.IsRepeating = true;

        activeTimers[windowHandle] = timer;

        timer.Tick += (sender, args) =>
        {
            step++;

            float progress = Math.Clamp((float)step / totalSteps, 0f, 1f);
            float eased = EaseInOut(progress);
            float alpha = from + (to - from) * eased;

            PInvoke.SetLayeredWindowAttributes(windowHandle, new COLORREF(0), (byte)(255 * alpha), LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);

            if (step >= totalSteps)
            {
                timer.Stop();
                activeTimers.Remove(windowHandle);
                completed?.Invoke();
            }
        };

        timer.Start();
    }

    private void AllowActivation()
    {
        WindowStyle exStyle = (WindowStyle)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        if ((exStyle & WindowStyle.NoActivate) == 0)
        {
            return;
        }

        PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            (int)(exStyle & ~WindowStyle.NoActivate));
    }

    private void RepositionToWorkArea()
    {
        if (window.AppWindow is null)
        {
            return;
        }

        RectInt32 workArea = GetMonitorWorkArea(currentMonitor);

        contentPresenter.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        int contentWidth = (int)Math.Ceiling(contentPresenter.DesiredSize.Width);
        int contentHeight = (int)Math.Ceiling(contentPresenter.DesiredSize.Height) + 1;
        int windowWidth = Math.Max(contentWidth, 1);
        int windowHeight = Math.Max(contentHeight, 1);
        int x = workArea.X + (workArea.Width - windowWidth) / 2;
        int y = placement == DesktopOverlayHeaderPlacement.Bottom
            ? workArea.Y + workArea.Height - windowHeight
            : workArea.Y;

        PInvoke.SetWindowPos(handle, HwndTopmost, x, y, windowWidth, windowHeight,
            SwpNoActivate);

        WindowExtensions.SetBorderless(handle, true);
    }

    private void StopAnimation(HWND windowHandle)
    {
        if (activeTimers.TryGetValue(windowHandle, out DispatcherQueueTimer? existing))
        {
            existing.Stop();
            activeTimers.Remove(windowHandle);
        }
    }
}
