using System.Runtime.InteropServices;

namespace Infinity.Platform.Windows;

internal static class NativeWindowCapture
{
    private const string LibraryName = "Infinity.Platform.Windows.Native.dll";

    public static bool IsAvailable()
    {
        try
        {
            return DwmThumbnailVisual_IsAvailable();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static bool TryBegin(nint targetWindowHandle)
    {
        try
        {
            return DwmThumbnailVisual_Begin(targetWindowHandle) == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static bool TryAdd(nint sourceWindowHandle, float x, float y, float width, float height, out int captureId)
    {
        captureId = -1;

        try
        {
            captureId = DwmThumbnailVisual_Add(sourceWindowHandle, x, y, width, height);
            return captureId >= 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static bool TryUpdate(int captureId, float x, float y, float width, float height)
    {
        try
        {
            return DwmThumbnailVisual_Update(captureId, x, y, width, height) == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static bool TryCommit()
    {
        try
        {
            return DwmThumbnailVisual_Commit() == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static bool TryClear()
    {
        try
        {
            return DwmThumbnailVisual_Clear() == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static int GetLastHResult()
    {
        try
        {
            return DwmThumbnailVisual_GetLastHResult();
        }
        catch (DllNotFoundException)
        {
            return unchecked((int)0x8007007E);
        }
        catch (EntryPointNotFoundException)
        {
            return unchecked((int)0x8007007F);
        }
    }

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DwmThumbnailVisual_IsAvailable();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int DwmThumbnailVisual_Begin(nint targetWindowHandle);

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int DwmThumbnailVisual_Add(nint sourceWindowHandle, float x, float y, float width, float height);

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int DwmThumbnailVisual_Update(int captureId, float x, float y, float width, float height);

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int DwmThumbnailVisual_Commit();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int DwmThumbnailVisual_Clear();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int DwmThumbnailVisual_GetLastHResult();
}
