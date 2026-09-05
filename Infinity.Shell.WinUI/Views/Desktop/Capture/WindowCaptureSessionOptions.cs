using System;
using System.Runtime.InteropServices;
using WinRT;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;

namespace Infinity.Shell.WinUI;

internal static unsafe class WindowCaptureSessionOptions
{
    public static void Apply(GraphicsCaptureSession session, bool borderless)
    {
        session.IsCursorCaptureEnabled = false;
        if (ApiInformation.IsPropertyPresent("Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired"))
        {
            session.IsBorderRequired = !borderless;
        }

        Guid interfaceId = new("D7419236-BE20-5E9F-BCD6-C4E98FD6AFDC");
        IObjectReference reference = ((IWinRTObject)session).NativeObject;
        int result = Marshal.QueryInterface(reference.ThisPtr, in interfaceId, out nint options);
        if (result == unchecked((int)0x80004002))
        {
            return;
        }

        Marshal.ThrowExceptionForHR(result);
        try
        {
            nint* table = *(nint**)options;
            delegate* unmanaged[Stdcall]<nint, byte, int> setIncludeSecondaryWindows = (delegate* unmanaged[Stdcall]<nint, byte, int> )table[7];
            Marshal.ThrowExceptionForHR(setIncludeSecondaryWindows(options, 0));
        }
        finally
        {
            Marshal.Release(options);
            GC.KeepAlive(reference);
        }
    }
}
