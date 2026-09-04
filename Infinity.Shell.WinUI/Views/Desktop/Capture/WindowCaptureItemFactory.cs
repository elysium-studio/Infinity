using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using WinRT;

namespace Infinity.Shell.WinUI;

// The documented HWND interop interface, not the private shared-DWM-visual API.
internal static unsafe class WindowCaptureItemFactory
{
    public static GraphicsCaptureItem Create(nint windowHandle)
    {
        Guid factoryId = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
        // IID_IGraphicsCaptureItem from windows.graphics.capture.h.
        Guid itemId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        nint className = 0;
        nint factory = 0;
        nint item = 0;
        const string runtimeClass = "Windows.Graphics.Capture.GraphicsCaptureItem";

        try
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(runtimeClass, runtimeClass.Length, out className));
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(className, in factoryId, out factory));
            nint* table = *(nint**)factory;
            var createForWindow = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)table[3];
            Marshal.ThrowExceptionForHR(createForWindow(factory, windowHandle, &itemId, &item));
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(item);
        }
        finally
        {
            if (item != 0) Marshal.Release(item);
            if (factory != 0) Marshal.Release(factory);
            if (className != 0) WindowsDeleteString(className);
        }
    }

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string value, int length, out nint result);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(nint className, in Guid interfaceId, out nint factory);
}
