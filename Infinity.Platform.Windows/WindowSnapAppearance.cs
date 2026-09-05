using System.Runtime.InteropServices;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Infinity.Platform.Windows;

public sealed unsafe class WindowSnapAppearance(ILogger<WindowSnapAppearance> logger) : IWindowSnapAppearance, IDisposable
{
    private const int CornerPreference = 33;
    private const uint DoNotRound = 1;
    private const string Marker = "Elysium.Infinity.SnapCornerPreference";
    private readonly HashSet<nint> modified = [];
    private readonly Lock gate = new();

    public bool TryApply(nint windowHandle)
    {
        lock (gate)
        {
            HWND hwnd = new(windowHandle);
            if (windowHandle == 0 || !PInvoke.IsWindow(hwnd))
            {
                return false;
            }

            uint original;
            fixed (char* name = Marker)
            {
                HANDLE saved = PInvoke.GetProp(hwnd, name);
                if (saved != default)
                {
                    modified.Add(windowHandle);
                    return true;
                }

                if (PInvoke.DwmGetWindowAttribute(hwnd, (DWMWINDOWATTRIBUTE)CornerPreference, &original, sizeof(uint)).Value < 0)
                {
                    return false;
                }

                if (original > 3 || !PInvoke.SetProp(hwnd, name, new HANDLE((nint)(original + 1))))
                {
                    return false;
                }

                uint square = DoNotRound;
                int result = DwmSetWindowAttribute(windowHandle, CornerPreference, in square, sizeof(uint));
                if (result < 0)
                {
                    PInvoke.RemoveProp(hwnd, name);
                    logger.LogDebug("Cannot set snapped window corners. Handle={Handle}, HRESULT=0x{Result:X8}", windowHandle, result);
                    return false;
                }

                modified.Add(windowHandle);
                return true;
            }
        }
    }


    public void Restore(nint windowHandle)
    {
        lock (gate)
        {
            HWND hwnd = new(windowHandle);
            fixed (char* name = Marker)
            {
                HANDLE saved = PInvoke.GetProp(hwnd, name);
                if (saved == default)
                {
                    modified.Remove(windowHandle);
                    return;
                }

                uint original = (uint)(nint)saved.Value - 1;
                int result = DwmSetWindowAttribute(windowHandle, CornerPreference, in original, sizeof(uint));
                if (result < 0)
                {
                    logger.LogDebug("Cannot restore window corners. Handle={Handle}, HRESULT=0x{Result:X8}", windowHandle, result);
                    return;
                }

                PInvoke.RemoveProp(hwnd, name);
                modified.Remove(windowHandle);
            }
        }
    }


    public void Dispose()
    {
        lock (gate)
        {
            foreach (nint handle in modified.ToArray())
            {
                Restore(handle);
            }
        }
    }


    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, in uint value, uint size);
}
