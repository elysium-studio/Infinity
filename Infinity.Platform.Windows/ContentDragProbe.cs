using System.Runtime.InteropServices;
using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

public sealed partial class ContentDragProbe : IDisposable
{
    private nint context;

    public void Start()
    {
        Dispose();
        Marshal.ThrowExceptionForHR(ContentDragProbe_Create(out context));
    }

    public ContentDragKind Poll() => (ContentDragKind)ContentDragProbe_Poll(context);

    public void Dispose()
    {
        if (context != 0)
        {
            ContentDragProbe_Destroy(context);
            context = 0;
        }
    }

    public static bool IsButtonDown => NativeDragPointer.IsButtonDown;

    [LibraryImport("Infinity.Platform.Windows.Native.dll")]
    private static partial int ContentDragProbe_Create(out nint context);

    [LibraryImport("Infinity.Platform.Windows.Native.dll")]
    private static partial uint ContentDragProbe_Poll(nint context);

    [LibraryImport("Infinity.Platform.Windows.Native.dll")]
    private static partial void ContentDragProbe_Destroy(nint context);
}
