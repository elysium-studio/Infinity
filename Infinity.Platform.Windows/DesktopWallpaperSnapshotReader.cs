using System.Runtime.InteropServices;
using Elysium.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace Infinity.Platform.Windows;

public sealed class DesktopWallpaperSnapshotReader(IWorkspace workspace)
{
    internal DesktopBackgroundSnapshot Read()
    {
        using DesktopWallpaperClient wallpaper = new();
        uint colour = wallpaper.GetBackgroundColor();
        string wallpaperPath = ReadWallpaperPath(wallpaper);
        return new(wallpaperPath, colour);
    }


    private string ReadWallpaperPath(DesktopWallpaperClient wallpaper)
    {
        DesktopWallpaperRect activeMonitor = GetActiveMonitorRect();
        uint monitorCount = wallpaper.GetMonitorCount();
        string? fallbackPath = null;
        for (uint monitorIndex = 0; monitorIndex < monitorCount; monitorIndex++)
        {
            string monitorId = wallpaper.GetMonitorId(monitorIndex);
            string path = wallpaper.GetWallpaper(monitorId);
            fallbackPath ??= path;
            DesktopWallpaperRect monitorRect = wallpaper.GetMonitorRect(monitorId);
            if (HasSameBounds(monitorRect, activeMonitor))
            {
                return path;
            }
        }

        return fallbackPath ?? string.Empty;
    }


    private static bool HasSameBounds(DesktopWallpaperRect first, DesktopWallpaperRect second) => first.Left == second.Left && first.Top == second.Top && first.Right == second.Right && first.Bottom == second.Bottom;

    private DesktopWallpaperRect GetActiveMonitorRect()
    {
        HMONITOR monitor = new(workspace.GetCurrentWorkspace());
        MONITORINFO monitorInfo = new()
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };
        if (!PInvoke.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return default;
        }

        return new(monitorInfo.rcMonitor.left, monitorInfo.rcMonitor.top, monitorInfo.rcMonitor.right, monitorInfo.rcMonitor.bottom);
    }
}
