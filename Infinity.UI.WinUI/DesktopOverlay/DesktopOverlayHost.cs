using Elysium.Platform.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;
using WinUIEx;
using WindowExtensions = Infinity.UI.WinUI.DesktopOverlayWindowStyle;
using WindowStyle = Infinity.UI.WinUI.DesktopOverlayExtendedWindowStyle;

namespace Infinity.UI.WinUI;

internal class DesktopOverlayHost
{
    private const SET_WINDOW_POS_FLAGS SwpNoActivate = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
    private const SET_WINDOW_POS_FLAGS SwpNoSize = SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
    private const SET_WINDOW_POS_FLAGS SwpNoMove = SET_WINDOW_POS_FLAGS.SWP_NOMOVE;
    private const uint WmLButtonDown = 0x0201u;
    private const uint WmRButtonDown = 0x0204u;

    private static readonly HWND HwndBottom = new(new nint(1));
    private static readonly HWND HwndNotTopmost = new(new nint(-2));
    private static readonly HWND HwndTopmost = new(new nint(-1));
    private static readonly List<HWND> globalExcludedHandles = [];
    private readonly List<(Window Window, RectInt32 Rect, SystemBackdropElement BackdropElement, Grid ContentRoot, HMONITOR Monitor)> windows = [];
    private readonly DesktopOverlayMonitorTopology monitorTopology = new();
    private readonly DesktopOverlay overlay;
    private readonly DesktopOverlayResponsivenessMonitor responsivenessMonitor;
    private readonly HOOKPROC mouseProc;
    private readonly Dictionary<HWND, DispatcherQueueTimer> windowOpacityTimers = [];
    private readonly Dictionary<HWND, float> windowOpacities = [];
    private readonly Lock hookLock = new();
    private UnhookWindowsHookExSafeHandle? mouseHook;
    private bool isVisible;
    private volatile bool isTopMost;
    private bool isInputEnabled;
    private bool isMonitorSpanningEnabled;
    private bool blurRequested = true;
    private volatile bool staysOpen;
    private bool disposed;
    private HMONITOR currentMonitor;
    private RectInt32 currentBounds;

    public event EventHandler? Dismissed;

    internal bool IsEmergencyHidden => responsivenessMonitor.IsEmergencyHidden;

    internal static void RegisterExcludedHandle(HWND handle)
    {
        if (!globalExcludedHandles.Contains(handle))
        {
            globalExcludedHandles.Add(handle);
        }
    }

    internal static void UnregisterExcludedHandle(HWND handle) =>
        globalExcludedHandles.Remove(handle);

    public DesktopOverlayHost(DesktopOverlay overlay)
    {
        this.overlay = overlay;
        mouseProc = MouseHookCallback;

        foreach (DesktopOverlayMonitor monitor in monitorTopology.Monitors)
        {
            Window window = CreateOverlayWindow();
            SystemBackdropElement backdropElement = new()
            {
                IsHitTestVisible = false,
                SystemBackdrop = new UntintedDesktopAcrylicBackdrop()
            };
            Grid contentRoot = new();
            Grid root = new();
            root.Children.Add(backdropElement);
            root.Children.Add(contentRoot);
            window.Content = root;

            windows.Add((window, monitor.Bounds, backdropElement, contentRoot, monitor.Handle));
            PrewarmWindow(window, monitor.Bounds, backdropElement, contentRoot);
            windowOpacities[new HWND(WindowNative.GetWindowHandle(window))] = 0;
        }

        if (windows.Count > 0)
        {
            currentMonitor = windows[0].Monitor;
            currentBounds = windows[0].Rect;
            windows[0].ContentRoot.Children.Insert(0, overlay);
        }

        ApplyBlurState();
        HWND[] handles = new HWND[windows.Count];
        for (int index = 0; index < windows.Count; index++)
            handles[index] = new HWND(WindowNative.GetWindowHandle(windows[index].Window));
        responsivenessMonitor = new(handles, overlay.DispatcherQueue, () =>
        {
            Debug.WriteLine("Overlay UI stopped responding; the native safety monitor hid it.");
            foreach (HWND handle in handles)
            {
                StopWindowOpacityAnimation(handle);
                windowOpacities[handle] = 0;
            }
            Hide();
            Dismissed?.Invoke(this, EventArgs.Empty);
        });
    }

    internal MonitorHandle CurrentMonitor => new MonitorHandle((nint)currentMonitor);

    internal RectInt32 ScreenBounds => currentBounds;

    internal RectInt32 CurrentMonitorBounds
    {
        get
        {
            foreach ((Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR monitor) in windows)
            {
                if (monitor == currentMonitor)
                {
                    return rect;
                }
            }

            return currentBounds;
        }
    }

    internal nint Handle
    {
        get
        {
            foreach ((Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR monitor) in windows)
            {
                if (monitor == currentMonitor)
                {
                    return WindowNative.GetWindowHandle(window);
                }
            }

            return windows.Count > 0 ? WindowNative.GetWindowHandle(windows[0].Window) : 0;
        }
    }

    public void SetStaysOpen(bool value)
    {
        staysOpen = value;
    }

    public void SetBlurEnabled(bool enabled)
    {
        blurRequested = enabled;
        ApplyBlurState();
    }

    public void SetInputEnabled(bool enabled)
    {
        isInputEnabled = enabled;

        foreach ((Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR monitor) in windows)
        {
            window.DispatcherQueue.TryEnqueue(() => ApplyInputState(window, monitor));
        }
    }

    public void SetMonitorSpanningEnabled(bool enabled)
    {
        isMonitorSpanningEnabled = enabled;

        if (isVisible)
        {
            Show();
        }
    }

    public void Show()
    {
        isVisible = true;
        responsivenessMonitor.Start();
        InstallMouseHook();

        currentMonitor = ResolveActiveMonitor();
        DesktopOverlayMonitorSpan monitorSpan = monitorTopology.ResolveSpan(currentMonitor, isMonitorSpanningEnabled);
        currentBounds = monitorSpan.Bounds;
        MoveOverlayToMonitor(currentMonitor);
        ApplyBlurState();

        foreach ((Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR monitor) in windows)
        {
            HWND handle = new(WindowNative.GetWindowHandle(window));
            bool isSpanningHost = monitor == currentMonitor;
            bool isCoveredBySpan = monitorSpan.IsSpanning && !isSpanningHost && monitorSpan.Contains(monitor);
            RectInt32 targetRect = isSpanningHost && monitorSpan.IsSpanning ? monitorSpan.Bounds : rect;
            window.DispatcherQueue.TryEnqueue(() =>
            {
                StopWindowOpacityAnimation(handle);
                float opacity = isCoveredBySpan ? 0 : 1;
                windowOpacities[handle] = opacity;
                ElementCompositionPreview.GetElementVisual(backdropElement).Opacity = opacity;
                PInvoke.SetLayeredWindowAttributes(handle, new COLORREF(0), isCoveredBySpan ? (byte)0 : byte.MaxValue,
                    LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
                window.AppWindow.MoveAndResize(targetRect);
                WindowExtensions.SetBorderless(handle, true);
                ApplyInputState(window, monitor);
                contentRoot.Visibility = isCoveredBySpan ? Visibility.Collapsed : Visibility.Visible;
                PInvoke.SetWindowPos(handle, isTopMost ? HwndTopmost : HwndBottom, 0, 0, 0, 0,
                    SwpNoActivate | SwpNoSize | SwpNoMove);
            });
        }
    }

    public void Hide()
    {
        responsivenessMonitor.Stop();
        isVisible = false;
        isTopMost = false;
        UninstallMouseHook();

        foreach ((Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR monitor) in windows)
        {
            HWND handle = new(WindowNative.GetWindowHandle(window));
            window.DispatcherQueue.TryEnqueue(() =>
            {
                StopWindowOpacityAnimation(handle);
                float opacity = windowOpacities.GetValueOrDefault(handle, 1);
                AnimateWindowOpacity(window.DispatcherQueue, handle, opacity, 0f, TimeSpan.FromMilliseconds(200), () =>
                {
                    contentRoot.Visibility = Visibility.Collapsed;
                    ElementCompositionPreview.GetElementVisual(backdropElement).Opacity = 0;
                    PInvoke.SetWindowPos(handle, HwndBottom, 0, 0, 0, 0,
                        SwpNoActivate | SwpNoSize | SwpNoMove);
                });
            });
        }
    }

    public void SetTopMost(bool enabled)
    {
        isTopMost = enabled;
        HWND insertAfter = enabled ? HwndTopmost : HwndNotTopmost;

        foreach ((Window window, _, _, _, _) in windows)
        {
            HWND handle = new(WindowNative.GetWindowHandle(window));
            PInvoke.SetWindowPos(
                handle,
                insertAfter,
                0,
                0,
                0,
                0,
                SwpNoActivate | SwpNoSize | SwpNoMove);
        }
    }

    internal Task CloseAsync()
    {
        responsivenessMonitor.Dispose();
        lock (hookLock)
        {
            if (disposed)
            {
                return Task.CompletedTask;
            }

            disposed = true;
        }

        UninstallMouseHook();
        (Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR monitor)[] snapshot = windows.ToArray();
        windows.Clear();

        if (snapshot.Length == 0)
        {
            return Task.CompletedTask;
        }

        DispatcherQueue queue = snapshot[0].window.DispatcherQueue;

        if (queue.HasThreadAccess)
        {
            CloseWindows(snapshot);
            return Task.CompletedTask;
        }

        TaskCompletionSource taskCompletionSource = new();

        bool enqueued = queue.TryEnqueue(() =>
        {
            try
            {
                CloseWindows(snapshot);
                taskCompletionSource.SetResult();
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }
        });

        if (!enqueued)
        {
            taskCompletionSource.SetResult();
        }

        return taskCompletionSource.Task;
    }

    private void CloseWindows((Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR monitor)[] snapshot)
    {
        foreach ((Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR monitor) in snapshot)
        {
            HWND handle = new(WindowNative.GetWindowHandle(window));
            StopWindowOpacityAnimation(handle);
            windowOpacities.Remove(handle);
            window.Close();
        }
    }

    internal void Close() => CloseAsync().GetAwaiter().GetResult();

    private void MoveOverlayToMonitor(HMONITOR monitor)
    {
        foreach ((Window window, _, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR candidateMonitor) in windows)
        {
            if (contentRoot.Children.Contains(overlay))
            {
                contentRoot.Children.Remove(overlay);
            }
        }

        Grid targetContentRoot = windows[0].ContentRoot;

        foreach ((Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR candidateMonitor) in windows)
        {
            if (candidateMonitor == monitor)
            {
                targetContentRoot = contentRoot;
                break;
            }
        }

        targetContentRoot.Children.Insert(0, overlay);
    }

    private static HMONITOR ResolveActiveMonitor()
    {
        PInvoke.GetCursorPos(out System.Drawing.Point cursor);

        return PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
    }

    private void ApplyBlurState()
    {
        bool requested = blurRequested;

        foreach ((Window window, RectInt32 rect, SystemBackdropElement backdropElement, Grid contentRoot, HMONITOR monitor) in windows)
        {
            void Apply()
            {
                bool acrylicApplied = backdropElement.SystemBackdrop is UntintedDesktopAcrylicBackdrop;

                if (requested && !acrylicApplied)
                {
                    backdropElement.SystemBackdrop = new UntintedDesktopAcrylicBackdrop();
                }
                else if (!requested && acrylicApplied)
                {
                    backdropElement.SystemBackdrop = null;
                }

            }

            if (window.DispatcherQueue.HasThreadAccess)
            {
                Apply();
            }
            else
            {
                window.DispatcherQueue.TryEnqueue(Apply);
            }
        }
    }

    private void InstallMouseHook()
    {
        lock (hookLock)
        {
            if (disposed || mouseHook is not null)
            {
                return;
            }

            mouseHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, mouseProc, null, 0);
        }
    }

    private void UninstallMouseHook()
    {
        lock (hookLock)
        {
            if (mouseHook is null)
            {
                return;
            }

            mouseHook.Dispose();
            mouseHook = null;
        }
    }

    private unsafe LRESULT MouseHookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && isVisible && !staysOpen)
        {
            uint message = (uint)(nuint)wParam;

            if (message == WmLButtonDown || message == WmRButtonDown)
            {
                MSLLHOOKSTRUCT hookStruct = *(MSLLHOOKSTRUCT*)(nint)lParam;
                HWND clickedWindow = PInvoke.WindowFromPoint(hookStruct.pt);
                HWND rootWindow = PInvoke.GetAncestor(clickedWindow, GET_ANCESTOR_FLAGS.GA_ROOT);
                bool isExcluded = globalExcludedHandles.Contains(clickedWindow) || globalExcludedHandles.Contains(rootWindow);

                if (!isExcluded)
                {
                    windows[0].Window.DispatcherQueue.TryEnqueue(() =>
                    {
                        Dismissed?.Invoke(this, EventArgs.Empty);
                    });
                }
            }
        }

        return PInvoke.CallNextHookEx(null, nCode, wParam, lParam);
    }

    private static Window CreateOverlayWindow()
    {
        Window window = new()
        {
            ExtendsContentIntoTitleBar = true,
            Content = new Grid()
        };

        window.SetTitleBar(null);
        HWND handle = new(WindowNative.GetWindowHandle(window));

        int style = PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        _ = PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE,
            style & ~0x00C00000 & ~0x00040000 & ~0x00080000);

        WindowStyle exStyle = (WindowStyle)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _ = PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            (int)(exStyle | WindowStyle.Transparent | WindowStyle.NoActivate | WindowStyle.ToolWindow | WindowStyle.Layered));

        PInvoke.SetLayeredWindowAttributes(handle, new COLORREF(0), 0, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        window.SystemBackdrop = new TransparentTintBackdrop();

        PInvoke.SetWindowPos(handle, HWND.Null, -32000, -32000, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);

        return window;
    }

    private static void PrewarmWindow(Window window,
        RectInt32 rect,
        SystemBackdropElement backdropElement,
        Grid contentRoot)
    {
        HWND handle = new(WindowNative.GetWindowHandle(window));
        backdropElement.Opacity = 1;
        contentRoot.Visibility = Visibility.Collapsed;
        PInvoke.SetLayeredWindowAttributes(handle, new COLORREF(0), 0,
            LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        window.AppWindow.MoveAndResize(rect);
        WindowExtensions.SetBorderless(handle, true);
        window.AppWindow.Show(activateWindow: false);
        PInvoke.SetWindowPos(handle, HwndBottom, 0, 0, 0, 0, SwpNoActivate | SwpNoSize | SwpNoMove);
    }

    private void ApplyInputState(Window window, HMONITOR monitor)
    {
        HWND handle = new(WindowNative.GetWindowHandle(window));
        WindowStyle style = (WindowStyle)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        WindowStyle updated = isInputEnabled && monitor == currentMonitor
            ? style & ~WindowStyle.Transparent
            : style | WindowStyle.Transparent;

        if (updated != style)
        {
            _ = PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)updated);
        }
    }

    private void StopWindowOpacityAnimation(HWND handle)
    {
        if (windowOpacityTimers.Remove(handle, out DispatcherQueueTimer? timer))
        {
            timer.Stop();
        }
    }

    private void AnimateWindowOpacity(DispatcherQueue dispatcherQueue,
        HWND handle,
        float from,
        float to,
        TimeSpan duration,
        Action completed)
    {
        long startedAt = Stopwatch.GetTimestamp();
        DispatcherQueueTimer timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(16);
        timer.IsRepeating = true;
        windowOpacityTimers[handle] = timer;

        timer.Tick += (sender, args) =>
        {
            float progress = Math.Clamp((float)(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds /
                duration.TotalMilliseconds), 0f, 1f);
            float eased = progress < 0.5f
                ? 2f * progress * progress
                : 1f - MathF.Pow(-2f * progress + 2f, 2f) / 2f;
            float opacity = from + (to - from) * eased;
            windowOpacities[handle] = opacity;
            PInvoke.SetLayeredWindowAttributes(handle, new COLORREF(0), (byte)(255 * opacity),
                LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);

            if (progress < 1f)
            {
                return;
            }

            timer.Stop();
            windowOpacityTimers.Remove(handle);
            completed();
        };

        timer.Start();
    }

}
