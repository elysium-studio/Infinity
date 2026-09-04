using Windows.Graphics.Capture;

namespace Infinity.Shell.WinUI;

internal static class WindowCaptureFrameReader
{
    public const int BufferCount = 2;

    public static Direct3D11CaptureFrame? TakeLatest(Direct3D11CaptureFramePool pool)
    {
        Direct3D11CaptureFrame? latest = null;
        try
        {
            // Bound the drain to the pool's capacity, so a busy source cannot
            // starve queued close/visibility work by producing frames forever.
            for (int index = 0; index < BufferCount; index++)
            {
                Direct3D11CaptureFrame? next = pool.TryGetNextFrame();
                if (next is null) break;
                Direct3D11CaptureFrame? previous = latest;
                latest = next;
                previous?.Dispose();
            }
            return latest;
        }
        catch
        {
            latest?.Dispose();
            throw;
        }
    }
}
