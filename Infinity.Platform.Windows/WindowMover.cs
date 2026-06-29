using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public unsafe class WindowMover :
    IWindowMover
{
    private const SET_WINDOW_POS_FLAGS SwpFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
        SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
        SET_WINDOW_POS_FLAGS.SWP_NOSENDCHANGING |
        SET_WINDOW_POS_FLAGS.SWP_DEFERERASE;

    private readonly List<PendingMove> pendingMoves = new();

    private HDWP deferWindowPosHandle;
    private bool batchFailed;

    public void BeginBatch(int count)
    {
        PInvoke.DwmFlush();

        pendingMoves.Clear();

        if (count <= 0)
        {
            deferWindowPosHandle = default;
            batchFailed = true;
            return;
        }

        deferWindowPosHandle = PInvoke.BeginDeferWindowPos(count);
        batchFailed = deferWindowPosHandle.Value == null;
    }

    public void MoveTo(nint windowHandle, int x, int y, int width, int height)
    {
        HWND hwnd = new(windowHandle);

        if (batchFailed)
        {
            PInvoke.SetWindowPos(hwnd, HWND.Null, x, y, width, height, SwpFlags);
            return;
        }

        HDWP result = PInvoke.DeferWindowPos(deferWindowPosHandle, hwnd, HWND.Null, x, y, width, height, SwpFlags);

        if (result.Value == null)
        {
            FallBackToIndividualMoves(hwnd, x, y, width, height);
            return;
        }

        deferWindowPosHandle = result;
        pendingMoves.Add(new PendingMove(hwnd, x, y, width, height));
    }

    public void EndBatch()
    {
        if (deferWindowPosHandle.Value != null && !batchFailed)
        {
            BOOL result = PInvoke.EndDeferWindowPos(deferWindowPosHandle);

            if (!result)
            {
                foreach (PendingMove pendingMove in pendingMoves)
                {
                    PInvoke.SetWindowPos(pendingMove.Hwnd, HWND.Null, pendingMove.X, pendingMove.Y, pendingMove.Width, pendingMove.Height, SwpFlags);
                }
            }
        }

        deferWindowPosHandle = default;
        batchFailed = false;
        pendingMoves.Clear();
    }

    public void Flush() { }

    private void FallBackToIndividualMoves(HWND failedHwnd, int failedX, int failedY, int failedWidth, int failedHeight)
    {
        deferWindowPosHandle = default;
        batchFailed = true;

        foreach (PendingMove pendingMove in pendingMoves)
        {
            PInvoke.SetWindowPos(pendingMove.Hwnd, HWND.Null, pendingMove.X, pendingMove.Y, pendingMove.Width, pendingMove.Height, SwpFlags);
        }

        pendingMoves.Clear();

        PInvoke.SetWindowPos(failedHwnd, HWND.Null, failedX, failedY, failedWidth, failedHeight, SwpFlags);
    }

    private readonly record struct PendingMove(HWND Hwnd, int X, int Y, int Width, int Height);
}