using Microsoft.UI.Composition;
using System;
using System.Runtime.InteropServices;
using WinRT;

namespace Infinity.Shell.WinUI;

public class SystemVisualProxyVisualPrivate :
    IDisposable
{
    private static readonly Guid SystemVisualProxyVisualPrivateStaticsId = new("6efeef10-e0c5-5997-bcb7-c1644f1cab81");
    private static readonly Guid SystemVisualProxyVisualPrivateInteropId = new("B2CFCBC2-7133-4EF8-A686-DB7FD4D536B4");
    private static readonly Guid VisualId = new("C0EEAB6C-C897-5AC6-A1C9-63ABD5055B9B");

    private nint proxyUnknown;

    private SystemVisualProxyVisualPrivate(nint proxyUnknown, Visual visual, nint handle)
    {
        this.proxyUnknown = proxyUnknown;
        Visual = visual;
        Handle = handle;
    }

    public Visual Visual { get; }

    public nint Handle { get; }

    public static SystemVisualProxyVisualPrivate Create(Compositor compositor)
    {
        nint classId = 0;
        nint factory = 0;
        nint statics = 0;
        nint proxyUnknown = 0;
        nint proxyVisual = 0;
        IObjectReference? compositorReference = null;

        try
        {
            classId = CreateHString("Microsoft.UI.Composition.Private.SystemVisualProxyVisualPrivate");
            ThrowIfFailed(DllGetActivationFactory(classId, out factory));

            Guid staticsId = SystemVisualProxyVisualPrivateStaticsId;
            ThrowIfFailed(Marshal.QueryInterface(factory, ref staticsId, out statics));

            compositorReference = ((IWinRTObject)compositor).NativeObject;

            CreateProxyDelegate createProxy = GetDelegate<CreateProxyDelegate>(statics, 6);
            ThrowIfFailed(createProxy(statics, compositorReference.ThisPtr, out proxyUnknown));

            Guid visualId = VisualId;
            ThrowIfFailed(Marshal.QueryInterface(proxyUnknown, ref visualId, out proxyVisual));

            Visual visual = Visual.FromAbi(proxyVisual);
            nint handle = GetHandle(proxyUnknown);

            return new SystemVisualProxyVisualPrivate(proxyUnknown, visual, handle);
        }
        catch
        {
            if (proxyUnknown != 0)
            {
                Marshal.Release(proxyUnknown);
            }

            throw;
        }
        finally
        {
            if (proxyVisual != 0)
            {
                Marshal.Release(proxyVisual);
            }

            if (classId != 0)
            {
                WindowsDeleteString(classId);
            }

            compositorReference = null;

            if (statics != 0)
            {
                Marshal.Release(statics);
            }

            if (factory != 0)
            {
                Marshal.Release(factory);
            }
        }
    }

    public void Dispose()
    {
        if (proxyUnknown != 0)
        {
            Marshal.Release(proxyUnknown);
            proxyUnknown = 0;
        }
    }

    private static nint GetHandle(nint proxyUnknown)
    {
        nint interop = 0;

        try
        {
            Guid interopId = SystemVisualProxyVisualPrivateInteropId;
            ThrowIfFailed(Marshal.QueryInterface(proxyUnknown, ref interopId, out interop));

            GetHandleDelegate getHandle = GetDelegate<GetHandleDelegate>(interop, 3);
            ThrowIfFailed(getHandle(interop, out nint handle));

            return handle;
        }
        finally
        {
            if (interop != 0)
            {
                Marshal.Release(interop);
            }
        }
    }

    private static TDelegate GetDelegate<TDelegate>(nint instance, int index)
        where TDelegate : Delegate
    {
        nint table = Marshal.ReadIntPtr(instance);
        nint function = Marshal.ReadIntPtr(table, index * IntPtr.Size);

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(function);
    }

    private static nint CreateHString(string value)
    {
        ThrowIfFailed(WindowsCreateString(value, value.Length, out nint hstring));
        return hstring;
    }

    private static void ThrowIfFailed(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateProxyDelegate(nint instance, nint compositor, out nint proxyVisual);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetHandleDelegate(nint instance, out nint handle);

    [DllImport("dcompi.dll", EntryPoint = "DllGetActivationFactory", CallingConvention = CallingConvention.StdCall)]
    private static extern int DllGetActivationFactory(nint activatableClassId, out nint factory);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string sourceString, int length, out nint hstring);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int WindowsDeleteString(nint hstring);
}
