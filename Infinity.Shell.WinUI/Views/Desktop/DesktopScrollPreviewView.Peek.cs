using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopScrollPreviewView
{
    private nint peekWindowHandle;
    private DesktopWindowPreview? peekPreview;
    private IDisposable? peekScrollSuppression;

    private void HandleWindowPeekRequested(nint handle)
    {
        if (!isRunning || !interactionReady || !overviewConfiguration.ShowSearchBox || string.IsNullOrWhiteSpace(WindowSearchBox.Text) || contentDragEnabled || previews.HasActiveInteraction || ApplicationPickerFlyout.IsOpen || pageStrip.IsEditorActive)
        {
            return;
        }

        if (peekWindowHandle == handle)
        {
            DismissPeek();
            return;
        }

        ClosePeek();
        if (!previews.TryGet(handle, out DesktopWindowPreview? preview) || preview is null || !preview.CanPeek)
        {
            return;
        }

        DesktopWindowPeekBounds source = GetPeekSourceBounds(handle);
        if (!source.IsValid)
        {
            return;
        }

        try
        {
            peekWindowHandle = handle;
            peekPreview = preview;
            peekScrollSuppression = scrollInputSuppression.Suppress();
            preview.SetPeeking(true);
            PeekView.Begin(preview.Host, source);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not bring the thumbnail into peek view");
            ClosePeek();
        }
    }

    public bool TryDismissPeek()
    {
        if (peekWindowHandle == 0)
        {
            return false;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            DismissPeek();
        }
        else
        {
            DispatcherQueue.TryEnqueue(DismissPeek);
        }
        return true;
    }

    private void ClosePeek()
    {
        PeekView.Hide();
        peekPreview?.SetPeeking(false);
        peekPreview = null;
        peekWindowHandle = 0;
        peekScrollSuppression?.Dispose();
        peekScrollSuppression = null;
    }

    private void DismissPeek()
    {
        if (peekWindowHandle != 0)
        {
            PeekView.AnimateOut(GetPeekSourceBounds(peekWindowHandle), ClosePeek);
        }
    }

    private DesktopWindowPeekBounds GetPeekSourceBounds(nint handle)
    {
        if (!previews.TryGet(handle, out DesktopWindowPreview? preview) || preview is null || preview.Host.XamlRoot != XamlRoot)
        {
            return default;
        }

        Rect bounds = preview.Host.TransformToVisual(this).TransformBounds(new Rect(0, 0, preview.Host.ActualWidth, preview.Host.ActualHeight));
        return new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private void HandlePeekInvoked()
    {
        nint handle = peekWindowHandle;
        ClosePeek();
        if (handle != 0 && windowCollection.AllTrackedWindows.Any(window => window.Handle == handle))
        {
            HandleWindowInvoked(handle);
        }
    }

    private void RefreshPeek()
    {
        if (peekWindowHandle != 0 && (!previews.TryGet(peekWindowHandle, out DesktopWindowPreview? current) || !ReferenceEquals(current, peekPreview) || current is null || !PeekView.MatchesSourceSize(current.Host)))
        {
            ClosePeek();
        }
    }

    private void HandleOverviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape && TryDismissPeek())
        {
            args.Handled = true;
        }
    }
}
