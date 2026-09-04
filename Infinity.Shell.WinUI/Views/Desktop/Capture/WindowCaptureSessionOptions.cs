using System;
using System.Runtime.InteropServices;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using WinRT;

namespace Infinity.Shell.WinUI;

internal static unsafe class WindowCaptureSessionOptions
{
    public static void Apply(GraphicsCaptureSession session, bool borderless)
    {
        session.IsCursorCaptureEnabled = false;
        if (ApiInformation.IsPropertyPresent("Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired"))
            session.IsBorderRequired = !borderless;

        // Public Windows SDK IGraphicsCaptureSession6. Optional QI preserves
        // the app's Windows 11 minimum target without a private compositor API.
        // Newer Windows can explicitly exclude owned popups/debugger adorners.
        Guid interfaceId = new("D7419236-BE20-5E9F-BCD6-C4E98FD6AFDC");
        IObjectReference reference = ((IWinRTObject)session).NativeObject;
        int result = Marshal.QueryInterface(reference.ThisPtr, in interfaceId, out nint options);
        if (result == unchecked((int)0x80004002)) return;
        Marshal.ThrowExceptionForHR(result);
        try
        {
            nint* table = *(nint**)options;
            var setIncludeSecondaryWindows = (delegate* unmanaged[Stdcall]<nint, byte, int>)table[7];
            Marshal.ThrowExceptionForHR(setIncludeSecondaryWindows(options, 0));
        }
        finally
        {
            Marshal.Release(options);
            GC.KeepAlive(reference);
        }
    }
}
