using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class WindowRestoreGuard :
    IWindowRestoreGuard
{
    private readonly HashSet<IntPtr> restoringWindows = [];

    public bool IsRestoring(IntPtr windowHandle) => restoringWindows.Contains(windowHandle);

    public void MarkRestoring(IntPtr windowHandle)
    {
        restoringWindows.Add(windowHandle);
        Task.Delay(500).ContinueWith(_ => restoringWindows.Remove(windowHandle));
    }
}