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
            for (int index = 0; index < BufferCount; index++)
            {
                Direct3D11CaptureFrame? next = pool.TryGetNextFrame();
                if (next is null)
                {
                    break;
                }

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
