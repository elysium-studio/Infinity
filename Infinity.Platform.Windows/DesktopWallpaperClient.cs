using System.Runtime.InteropServices;

namespace Infinity.Platform.Windows;

internal sealed unsafe class DesktopWallpaperClient :
    IDisposable
{
    private const uint ClassContextInProcessOrLocalServer = 5;
    private const uint MultiThreadedApartment = 0;
    private const int RpcChangedMode = unchecked((int)0x80010106);
    private const int ReleaseSlot = 2;
    private const int GetWallpaperSlot = 4;
    private const int GetMonitorDevicePathAtSlot = 5;
    private const int GetMonitorDevicePathCountSlot = 6;
    private const int GetMonitorRectSlot = 7;
    private const int GetBackgroundColorSlot = 9;

    private static readonly Guid DesktopWallpaperClassId = new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD");
    private static readonly Guid DesktopWallpaperInterfaceId = new("B92B56A9-8B55-4E14-9A89-0199BBB6F93B");

    private readonly bool uninitialiseApartment;
    private nint instance;

    public DesktopWallpaperClient()
    {
        int apartmentResult = DesktopWallpaperNativeMethods.CoInitializeEx(0, MultiThreadedApartment);

        if (apartmentResult < 0 && apartmentResult != RpcChangedMode)
        {
            ThrowForResult(apartmentResult);
        }

        uninitialiseApartment = apartmentResult >= 0;
        int creationResult = DesktopWallpaperNativeMethods.CoCreateInstance(
            in DesktopWallpaperClassId,
            0,
            ClassContextInProcessOrLocalServer,
            in DesktopWallpaperInterfaceId,
            out instance);

        if (creationResult < 0)
        {
            if (uninitialiseApartment)
            {
                DesktopWallpaperNativeMethods.CoUninitialize();
            }

            ThrowForResult(creationResult);
        }
    }

    public uint GetMonitorCount()
    {
        uint count = 0;
        nint method = GetMethod(GetMonitorDevicePathCountSlot);
        int result = ((delegate* unmanaged<nint, uint*, int>)method)(instance, &count);
        ThrowForResult(result);
        return count;
    }

    public string GetMonitorId(uint monitorIndex)
    {
        nint value = 0;
        nint method = GetMethod(GetMonitorDevicePathAtSlot);
        int result = ((delegate* unmanaged<nint, uint, nint*, int>)method)(instance, monitorIndex, &value);
        return ReadAllocatedString(result, value);
    }

    public string GetWallpaper(string monitorId)
    {
        nint value = 0;

        fixed (char* id = monitorId)
        {
            nint method = GetMethod(GetWallpaperSlot);
            int result = ((delegate* unmanaged<nint, char*, nint*, int>)method)(instance, id, &value);
            return ReadAllocatedString(result, value);
        }
    }

    public DesktopWallpaperRect GetMonitorRect(string monitorId)
    {
        DesktopWallpaperRect displayRect = default;

        fixed (char* id = monitorId)
        {
            nint method = GetMethod(GetMonitorRectSlot);
            int result = ((delegate* unmanaged<nint, char*, DesktopWallpaperRect*, int>)method)(instance, id, &displayRect);
            ThrowForResult(result);
        }

        return displayRect;
    }

    public uint GetBackgroundColor()
    {
        uint colour = 0;
        nint method = GetMethod(GetBackgroundColorSlot);
        int result = ((delegate* unmanaged<nint, uint*, int>)method)(instance, &colour);
        ThrowForResult(result);
        return colour;
    }

    public void Dispose()
    {
        if (instance == 0)
        {
            return;
        }

        nint method = GetMethod(ReleaseSlot);
        _ = ((delegate* unmanaged<nint, uint>)method)(instance);
        instance = 0;

        if (uninitialiseApartment)
        {
            DesktopWallpaperNativeMethods.CoUninitialize();
        }

        GC.SuppressFinalize(this);
    }

    private nint GetMethod(int slot)
    {
        nint* virtualTable = *(nint**)instance;
        return virtualTable[slot];
    }

    private static string ReadAllocatedString(int result, nint value)
    {
        try
        {
            ThrowForResult(result);
            return Marshal.PtrToStringUni(value) ?? string.Empty;
        }
        finally
        {
            if (value != 0)
            {
                DesktopWallpaperNativeMethods.CoTaskMemFree(value);
            }
        }
    }

    private static void ThrowForResult(int result)
    {
        if (result < 0)
        {
            throw new COMException("The desktop wallpaper service returned an error", result);
        }
    }
}
