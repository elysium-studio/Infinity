using System.Diagnostics;
using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class WindowRestoreGuard : IWindowRestoreGuard
{
    private static readonly long RestoreWindowDurationTicks = Stopwatch.Frequency / 2;
    private readonly Lock syncRoot = new();
    private readonly Dictionary<IntPtr, long> restoringWindows = [];

    public bool IsRestoring(IntPtr windowHandle)
    {
        lock (syncRoot)
        {
            if (!restoringWindows.TryGetValue(windowHandle, out long expiresAt))
            {
                return false;
            }

            if (Stopwatch.GetTimestamp() < expiresAt)
            {
                return true;
            }

            restoringWindows.Remove(windowHandle);
            return false;
        }
    }


    public void MarkRestoring(IntPtr windowHandle)
    {
        long expiresAt = Stopwatch.GetTimestamp() + RestoreWindowDurationTicks;
        lock (syncRoot)
        {
            restoringWindows[windowHandle] = expiresAt;
        }
    }
}
