using Infinity.Platform.Abstractions;
using System;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public class WindowController :
    IWindowController
{
    private const uint WmClose = 0x0010u;
    private const uint WmSysCommand = 0x0112u;
    private const nint ScMinimize = 0xF020;
    private const nint ScRestore = 0xF120;

    public void Close(nint handle) => PInvoke.PostMessage(new HWND(handle), WmClose, new WPARAM(0), new LPARAM(0));

    public void Minimize(nint handle) => PInvoke.PostMessage(new HWND(handle), WmSysCommand, new WPARAM((nuint)ScMinimize), new LPARAM(0));

    public void Restore(nint handle) => PInvoke.PostMessage(new HWND(handle), WmSysCommand, new WPARAM((nuint)ScRestore), new LPARAM(0));
}