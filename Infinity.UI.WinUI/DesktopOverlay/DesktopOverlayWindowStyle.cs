using System.Runtime.InteropServices;

namespace Infinity.UI.WinUI;

internal static partial class DesktopOverlayWindowStyle
{
    private const int GwlExStyle = -20;
    private const int GwlStyle = -16;
    private const int DwmwaWindowCornerPreference = 33;
    private const int SwpNoActivate = 0x0010;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoSize = 0x0001;
    private const int WsCaption = 0x00C00000;
    private const int WsDlgModalFrame = 0x00000001;
    private const int WsClientEdge = 0x00000200;
    private const int WsMaximizeBox = 0x00010000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsStaticEdge = 0x00020000;
    private const int WsSysMenu = 0x00080000;
    private const int WsThickFrame = 0x00040000;

    private static readonly nint HwndNotTopmost = new(-2);
    private static readonly nint HwndTopmost = new(-1);

    public static void SetBorderless(nint handle, bool enabled)
    {
        int style = GetWindowLong(handle, GwlStyle);
        int extendedStyle = GetWindowLong(handle, GwlExStyle);

        if (enabled)
        {
            style &= ~(WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox);
            extendedStyle &= ~(WsDlgModalFrame | WsClientEdge | WsStaticEdge);
        }
        else
        {
            style |= WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox;
            extendedStyle |= WsClientEdge;
        }

        _ = SetWindowLong(handle, GwlStyle, style);
        _ = SetWindowLong(handle, GwlExStyle, extendedStyle);
    }

    public static void SetSharpCorners(nint handle)
    {
        const uint sharpCornerPreference = 1;
        uint value = sharpCornerPreference;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref value, sizeof(uint));
    }

    public static void SetTopMost(nint handle, bool enabled) => SetWindowPos(handle, enabled ? HwndTopmost : HwndNotTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint handle, int attribute, ref uint value, int size);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(nint handle, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(nint handle, int index, int value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint handle, nint insertAfter, int x, int y, int width, int height, int flags);
}
