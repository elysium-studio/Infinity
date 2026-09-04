using Microsoft.Graphics.Canvas;
using Microsoft.UI.Composition;
using System;
using System.Runtime.InteropServices;
using WinRT;

namespace Infinity.Shell.WinUI;

internal static unsafe class WindowCaptureSwapChainInterop
{
    public static ICompositionSurface CreateSurface(Compositor compositor, CanvasSwapChain swapChain)
    {
        // Public interfaces from Microsoft.Graphics.Canvas.native.h and
        // Microsoft.UI.Composition.Interop.h. Win2D's managed WinUI build does
        // not expose the UWP CreateCompositionSurfaceForSwapChain convenience API.
        Guid wrapperId = new("5F10688D-EA55-4D55-A3B0-4DDB55C0C20A");
        Guid swapChainId = new("790A45F7-0D42-4876-983A-0A55CFE6F4AA");
        Guid compositorId = new("FC084699-67D8-40E1-ADE7-08901D84FFDA");
        IObjectReference canvasReference = ((IWinRTObject)swapChain).NativeObject;
        IObjectReference compositorReference = ((IWinRTObject)compositor).NativeObject;
        nint wrapper = 0;
        nint nativeSwapChain = 0;
        nint interop = 0;
        nint surface = 0;
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(canvasReference.ThisPtr, in wrapperId, out wrapper));
            nint* wrapperTable = *(nint**)wrapper;
            var getResource = (delegate* unmanaged[Stdcall]<nint, nint, float, Guid*, nint*, int>)wrapperTable[3];
            Marshal.ThrowExceptionForHR(getResource(wrapper, 0, 96, &swapChainId, &nativeSwapChain));

            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(compositorReference.ThisPtr, in compositorId, out interop));
            nint* compositorTable = *(nint**)interop;
            // IUnknown(3), CreateGraphicsDevice, CreateSurfaceForHandle, then SwapChain.
            var createSurface = (delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)compositorTable[5];
            Marshal.ThrowExceptionForHR(createSurface(interop, nativeSwapChain, &surface));
            return MarshalInterface<ICompositionSurface>.FromAbi(surface);
        }
        finally
        {
            if (surface != 0) Marshal.Release(surface);
            if (interop != 0) Marshal.Release(interop);
            if (nativeSwapChain != 0) Marshal.Release(nativeSwapChain);
            if (wrapper != 0) Marshal.Release(wrapper);
            GC.KeepAlive(canvasReference);
            GC.KeepAlive(compositorReference);
        }
    }
}
