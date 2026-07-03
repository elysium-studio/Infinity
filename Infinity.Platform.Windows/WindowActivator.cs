using Infinity.Platform.Abstractions;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public class WindowActivator :
    IWindowActivator
{
    private static readonly TimeSpan ActivationRetryDelay = TimeSpan.FromMilliseconds(60);

    public void Activate(nint handle)
    {
        if (handle == 0)
        {
            return;
        }

        HWND hwnd = new(handle);

        if (!PInvoke.IsWindow(hwnd))
        {
            return;
        }

        RestoreAndActivate(hwnd);

        _ = RetryRestoreAndActivateAsync(hwnd);
    }

    private static async Task RetryRestoreAndActivateAsync(HWND hwnd)
    {
        try
        {
            await Task.Delay(ActivationRetryDelay).ConfigureAwait(false);

            if (!PInvoke.IsWindow(hwnd))
            {
                return;
            }

            RestoreAndActivate(hwnd);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void RestoreAndActivate(HWND hwnd)
    {
        if (PInvoke.IsIconic(hwnd))
        {
            PInvoke.ShowWindowAsync(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
        }
        else
        {
            PInvoke.ShowWindowAsync(hwnd, SHOW_WINDOW_CMD.SW_SHOW);
        }

        PInvoke.SetForegroundWindow(hwnd);
    }
}