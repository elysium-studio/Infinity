using Infinity.Application.Abstractions;
using System.Diagnostics;

namespace Infinity.Application;

public sealed class WindowPageTransitionGuard :
    IWindowPageTransitionGuard
{
    private static readonly long TransitionDurationTicks = Stopwatch.Frequency;

    private readonly Lock syncRoot = new();
    private readonly Dictionary<nint, PreservedPage> preservedPages = [];

    public void PreservePage(nint windowHandle, int page, int workspaceWidth, int workAreaX)
    {
        if (windowHandle == 0 || page < 0 || workspaceWidth <= 0)
        {
            return;
        }

        PreservedPage preservedPage = new(
            page,
            workspaceWidth,
            workAreaX,
            Stopwatch.GetTimestamp() + TransitionDurationTicks);

        lock (syncRoot)
        {
            preservedPages[windowHandle] = preservedPage;
        }
    }

    public bool TryMapToPreservedPage(nint windowHandle, int candidateCanvasX, int windowWidth, out int mappedCanvasX)
    {
        lock (syncRoot)
        {
            if (!preservedPages.TryGetValue(windowHandle, out PreservedPage preservedPage))
            {
                mappedCanvasX = candidateCanvasX;
                return false;
            }

            if (Stopwatch.GetTimestamp() >= preservedPage.ExpiresAt)
            {
                preservedPages.Remove(windowHandle);
                mappedCanvasX = candidateCanvasX;
                return false;
            }

            double center = candidateCanvasX - preservedPage.WorkAreaX + (Math.Max(1, windowWidth) / 2d);
            int candidatePage = Math.Max(0, (int)Math.Floor(center / preservedPage.WorkspaceWidth));
            long pageDelta = (long)(preservedPage.Page - candidatePage) * preservedPage.WorkspaceWidth;
            mappedCanvasX = (int)Math.Clamp(candidateCanvasX + pageDelta, int.MinValue, int.MaxValue);
            return true;
        }
    }

    public void Clear(nint windowHandle)
    {
        lock (syncRoot)
        {
            preservedPages.Remove(windowHandle);
        }
    }

    private readonly record struct PreservedPage(
        int Page,
        int WorkspaceWidth,
        int WorkAreaX,
        long ExpiresAt);
}
