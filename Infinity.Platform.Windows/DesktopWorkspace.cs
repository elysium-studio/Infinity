using Elysium.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace Infinity.Platform.Windows;

public sealed class DesktopWorkspace :
    IWorkspace,
    IDisposable
{
    private readonly ILogger<DesktopWorkspace> logger;
    private bool disposed;

    public DesktopWorkspace(ILogger<DesktopWorkspace> logger)
    {
        this.logger = logger;
        SystemEvents.DisplaySettingsChanged += HandleWorkspaceLayoutChanged;
        SystemEvents.UserPreferenceChanged += HandleUserPreferenceChanged;

        WorkspaceBounds bounds = GetWorkspaceBounds();
        logger.LogInformation("Desktop workspace initialised — {Width}x{Height} at ({X}, {Y})", bounds.Width, bounds.Height, bounds.X, bounds.Y);
    }

    public event EventHandler? WorkspaceLayoutChanged;

    public int Width => GetWorkspaceBounds().Width;

    public int Height => GetWorkspaceBounds().Height;

    public int WorkAreaX => GetWorkspaceBounds().X;

    public int WorkAreaY => GetWorkspaceBounds().Y;

    public nint GetCurrentWorkspace()
    {
        PInvoke.GetCursorPos(out System.Drawing.Point cursor);
        return PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        SystemEvents.DisplaySettingsChanged -= HandleWorkspaceLayoutChanged;
        SystemEvents.UserPreferenceChanged -= HandleUserPreferenceChanged;
        GC.SuppressFinalize(this);
    }

    private void HandleWorkspaceLayoutChanged(object? sender, EventArgs args) => PublishWorkspaceLayoutChanged();

    private void HandleUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs args) => PublishWorkspaceLayoutChanged();

    private void PublishWorkspaceLayoutChanged()
    {
        WorkspaceBounds bounds = GetWorkspaceBounds();
        logger.LogInformation("Desktop workspace changed — {Width}x{Height} at ({X}, {Y})", bounds.Width, bounds.Height, bounds.X, bounds.Y);
        WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private static WorkspaceBounds GetWorkspaceBounds()
    {
        PInvoke.GetCursorPos(out System.Drawing.Point cursor);
        HMONITOR monitor = PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MONITORINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };

        if (!PInvoke.GetMonitorInfo(monitor, ref info))
        {
            return default;
        }

        return new WorkspaceBounds(info.rcWork.left, info.rcWork.top, info.rcWork.right - info.rcWork.left, info.rcWork.bottom - info.rcWork.top);
    }

    private readonly record struct WorkspaceBounds(int X, int Y, int Width, int Height);
}
