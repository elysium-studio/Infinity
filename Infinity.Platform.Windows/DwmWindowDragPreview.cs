using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Infinity.Platform.Windows;

public class DwmWindowDragPreviewFactory(ILogger<DwmWindowDragPreviewFactory> logger) :
    IWindowDragPreviewFactory
{
    public IWindowDragPreview? Create(nint ownerWindowHandle,
        nint sourceWindowHandle,
        WindowPreviewBounds bounds)
    {
        if (ownerWindowHandle == 0 || sourceWindowHandle == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        try
        {
            return new DwmWindowDragPreview(ownerWindowHandle, sourceWindowHandle, bounds);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to create the DWM drag preview");
            return null;
        }
    }
}

internal sealed class DwmWindowDragPreview :
    IWindowDragPreview
{
    private const uint DwmThumbnailDestination = 0x00000001;
    private const uint DwmThumbnailOpacity = 0x00000004;
    private const uint DwmThumbnailVisible = 0x00000008;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionShowWindow = 0x0040;
    private const uint WindowExNoActivate = 0x08000000;
    private const uint WindowExToolWindow = 0x00000080;
    private const uint WindowPopup = 0x80000000;

    private readonly nint ownerWindowHandle;
    private nint thumbnailHandle;
    private nint windowHandle;
    private int width;
    private int height;

    public DwmWindowDragPreview(nint ownerWindowHandle,
        nint sourceWindowHandle,
        WindowPreviewBounds bounds)
    {
        this.ownerWindowHandle = ownerWindowHandle;

        Point screenPosition = ToScreen(bounds);
        windowHandle = CreateWindowEx(WindowExNoActivate | WindowExToolWindow,
            "Static",
            null,
            WindowPopup,
            screenPosition.X,
            screenPosition.Y,
            bounds.Width,
            bounds.Height,
            ownerWindowHandle,
            0,
            0,
            0);

        if (windowHandle == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            Marshal.ThrowExceptionForHR(DwmRegisterThumbnail(windowHandle,
                sourceWindowHandle,
                out thumbnailHandle));
            ApplySize(bounds.Width, bounds.Height);
            Move(bounds);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Move(WindowPreviewBounds bounds)
    {
        if (windowHandle == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (width != bounds.Width || height != bounds.Height)
        {
            ApplySize(bounds.Width, bounds.Height);
        }

        Point screenPosition = ToScreen(bounds);

        if (!SetWindowPos(windowHandle,
            0,
            screenPosition.X,
            screenPosition.Y,
            bounds.Width,
            bounds.Height,
            SetWindowPositionNoActivate | SetWindowPositionShowWindow))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Dispose()
    {
        nint currentThumbnailHandle = Interlocked.Exchange(ref thumbnailHandle, 0);

        if (currentThumbnailHandle != 0)
        {
            _ = DwmUnregisterThumbnail(currentThumbnailHandle);
        }

        nint currentWindowHandle = Interlocked.Exchange(ref windowHandle, 0);

        if (currentWindowHandle != 0)
        {
            _ = DestroyWindow(currentWindowHandle);
        }

        GC.SuppressFinalize(this);
    }

    private void ApplySize(int width, int height)
    {
        DwmThumbnailProperties properties = new()
        {
            Flags = DwmThumbnailDestination | DwmThumbnailOpacity | DwmThumbnailVisible,
            Destination = new Rectangle(0, 0, width, height),
            Opacity = byte.MaxValue,
            IsVisible = true
        };

        Marshal.ThrowExceptionForHR(DwmUpdateThumbnailProperties(thumbnailHandle, in properties));
        ApplyWindowRegion(width, height);
        this.width = width;
        this.height = height;
    }

    private void ApplyWindowRegion(int width, int height)
    {
        int diameter = Math.Min(16, Math.Min(width, height));
        nint region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);

        if (region == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (SetWindowRgn(windowHandle, region, true) == 0)
        {
            int error = Marshal.GetLastWin32Error();
            _ = DeleteObject(region);
            throw new Win32Exception(error);
        }
    }

    private Point ToScreen(WindowPreviewBounds bounds)
    {
        Point point = new(bounds.X, bounds.Y);

        if (!ClientToScreen(ownerWindowHandle, ref point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return point;
    }

    [DllImport("user32.dll", EntryPoint = "ClientToScreen", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint windowHandle, ref Point point);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extendedStyle,
        string className,
        string? windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parentWindowHandle,
        nint menuHandle,
        nint instanceHandle,
        nint parameter);

    [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn", SetLastError = true)]
    private static extern nint CreateRoundRectRgn(int left,
        int top,
        int right,
        int bottom,
        int ellipseWidth,
        int ellipseHeight);

    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint objectHandle);

    [DllImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint windowHandle);

    [DllImport("dwmapi.dll", EntryPoint = "DwmRegisterThumbnail", PreserveSig = true)]
    private static extern int DwmRegisterThumbnail(nint destinationWindowHandle,
        nint sourceWindowHandle,
        out nint thumbnailHandle);

    [DllImport("dwmapi.dll", EntryPoint = "DwmUnregisterThumbnail", PreserveSig = true)]
    private static extern int DwmUnregisterThumbnail(nint thumbnailHandle);

    [DllImport("dwmapi.dll", EntryPoint = "DwmUpdateThumbnailProperties", PreserveSig = true)]
    private static extern int DwmUpdateThumbnailProperties(nint thumbnailHandle,
        in DwmThumbnailProperties properties);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint windowHandle,
        nint insertAfterWindowHandle,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "SetWindowRgn", SetLastError = true)]
    private static extern int SetWindowRgn(nint windowHandle,
        nint regionHandle,
        [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmThumbnailProperties
    {
        public uint Flags;

        public Rectangle Destination;

        public Rectangle Source;

        public byte Opacity;

        [MarshalAs(UnmanagedType.Bool)]
        public bool IsVisible;

        [MarshalAs(UnmanagedType.Bool)]
        public bool IsSourceClientAreaOnly;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point(int x, int y)
    {
        public int X = x;

        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle(int left, int top, int right, int bottom)
    {
        public int Left = left;

        public int Top = top;

        public int Right = right;

        public int Bottom = bottom;
    }
}
