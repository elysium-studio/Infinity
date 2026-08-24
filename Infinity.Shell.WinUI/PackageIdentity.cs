using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Infinity.Shell.WinUI;

internal static partial class PackageIdentity
{
    private const int AppModelErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;

    public static bool IsPackaged
    {
        get
        {
            uint length = 0;
            int result = GetCurrentPackageFullName(ref length, 0);

            if (result == ErrorInsufficientBuffer)
            {
                return true;
            }

            if (result == AppModelErrorNoPackage)
            {
                return false;
            }

            throw new Win32Exception(result);
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial int GetCurrentPackageFullName(ref uint packageFullNameLength,
        nint packageFullName);
}