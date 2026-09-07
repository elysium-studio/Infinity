using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public sealed partial class WindowIconSource(IApplicationCatalog catalog, ILogger<WindowIconSource> logger) : IWindowIconSource
{
    public async Task<ApplicationIcon?> GetIconAsync(nint windowHandle)
    {
        try
        {
            ApplicationIcon? windowIcon = await Task.Run(() => GetWindowIcon(windowHandle)).ConfigureAwait(false);
            if (windowIcon is not null)
            {
                return windowIcon;
            }

            string? path = await Task.Run(() => GetExecutablePath(windowHandle)).ConfigureAwait(false);
            return string.IsNullOrEmpty(path) ? null : await catalog.GetIconAsync(new(path, string.Empty)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            logger.LogDebug(exception, "Could not read the icon for window {WindowHandle}", windowHandle);
            return null;
        }
    }

    private ApplicationIcon? GetWindowIcon(nint handle)
    {
        nint buffer = 0;
        try
        {
            int result = WindowIcon_GetIcon(handle, 32, out buffer, out int width, out int height);
            if (result < 0 || buffer == 0 || width <= 0 || height <= 0 || width > 256 || height > 256)
            {
                return null;
            }

            byte[] pixels = new byte[width * height * 4];
            Marshal.Copy(buffer, pixels, 0, pixels.Length);
            return new(width, height, pixels);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            logger.LogDebug(exception, "The native window icon provider is unavailable");
            return null;
        }
        finally
        {
            if (buffer != 0)
            {
                ApplicationCatalog_FreeIcon(buffer);
            }
        }
    }

    [LibraryImport("Infinity.Platform.Windows.Native.dll")]
    private static partial int WindowIcon_GetIcon(nint window, int requestedSize, out nint buffer, out int width, out int height);

    [LibraryImport("Infinity.Platform.Windows.Native.dll")]
    private static partial void ApplicationCatalog_FreeIcon(nint buffer);

    private static string? GetExecutablePath(nint handle)
    {
        PInvoke.GetWindowThreadProcessId(new HWND(handle), out uint processId);
        if (processId == 0)
        {
            return null;
        }

        using Process process = Process.GetProcessById((int)processId);
        return process.MainModule?.FileName;
    }
}
