using System.Runtime.InteropServices;

namespace Infinity.Platform.Windows;

internal static partial class DesktopWallpaperNativeMethods
{
    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(
        in Guid classId,
        nint outer,
        uint classContext,
        in Guid interfaceId,
        out nint instance);

    [LibraryImport("ole32.dll")]
    internal static partial int CoInitializeEx(nint reserved, uint concurrencyModel);

    [LibraryImport("ole32.dll")]
    internal static partial void CoTaskMemFree(nint memory);

    [LibraryImport("ole32.dll")]
    internal static partial void CoUninitialize();
}
