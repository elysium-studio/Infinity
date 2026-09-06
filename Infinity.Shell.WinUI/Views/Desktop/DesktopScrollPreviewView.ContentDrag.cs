using System;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopScrollPreviewView
{
    private bool contentDragEnabled;
    private bool liveWindowDragEnabled;
    private bool contentDragInside;
    private bool interactionReady;
    private Point contentDragPoint;
    private IDisposable? contentDragScrollSuppression;

    internal bool IsContentDragReady => contentDragEnabled && interactionReady && contentDragInside;

    internal ContentDragKind DraggedContentKind { get; private set; }

    internal void SetLiveWindowDragEnabled(bool enabled)
    {
        SetContentDragEnabled(enabled);
        liveWindowDragEnabled = enabled;
        ContentDragSurface.AllowDrop = !enabled;
    }

    internal bool TryGetLiveWindowDragTarget(int screenX, int screenY, out DesktopContentDragTarget target)
    {
        double scale = XamlRoot?.RasterizationScale ?? 1;
        contentDragPoint = new((screenX - overlayScreenOriginX) / scale, (screenY - overlayScreenOriginY) / scale);
        contentDragInside = liveWindowDragEnabled;
        return TryGetContentDragTarget(out target);
    }

    internal void SetContentDragEnabled(bool enabled)
    {
        contentDragEnabled = enabled;
        contentDragInside = false;
        DraggedContentKind = ContentDragKind.None;
        ContentDragSurface.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        ContentDragOutline.Visibility = Visibility.Collapsed;
        if (enabled)
        {
            contentDragScrollSuppression ??= scrollInputSuppression.SuppressKeyboard();
            cursorConfinement.Release();
            SetInteractionEnabled(false);
        }
        else
        {
            contentDragScrollSuppression?.Dispose();
            contentDragScrollSuppression = null;
        }
    }

    private void HandleContentDragEntered(object sender, DragEventArgs args)
    {
        try
        {
            DraggedContentKind = DesktopContentDragFormats.Classify(args.DataView.AvailableFormats);
        }
        catch (Exception exception)
        {
            DraggedContentKind = ContentDragKind.Other;
            logger.LogDebug(exception, "The drag source did not expose its content formats.");
        }
        HandleContentDragOver(sender, args);
    }

    private void HandleContentDragOver(object sender, DragEventArgs args)
    {
        args.AcceptedOperation = DataPackageOperation.None;
        args.DragUIOverride.IsContentVisible = true;
        args.DragUIOverride.IsGlyphVisible = false;
        args.DragUIOverride.IsCaptionVisible = false;
        args.Handled = true;
        contentDragInside = contentDragEnabled;
        contentDragPoint = args.GetPosition(this);
    }

    private void HandleContentDragLeave(object sender, DragEventArgs args)
    {
        args.AcceptedOperation = DataPackageOperation.None;
        args.DragUIOverride.Clear();
        args.Handled = true;
        contentDragInside = false;
        ContentDragOutline.Visibility = Visibility.Collapsed;
    }

    internal bool TryGetContentDragTarget(out DesktopContentDragTarget target)
    {
        target = DesktopContentDragTarget.None;
        ContentDragOutline.Visibility = Visibility.Collapsed;
        if (!IsContentDragReady || !pageStrip.TryHitTestContentDrag(contentDragPoint, out int page, out Rect pageBounds))
        {
            return false;
        }

        double left = workAreaOffsetX + GetAnimationWidth() / 2 * (1 - animator.Scale);
        double top = workAreaOffsetY + GetAnimationHeight() / 2 * (1 - animator.Scale);
        double localX = (contentDragPoint.X - left) / animator.Scale;
        double localY = (contentDragPoint.Y - top) / animator.Scale;
        Rect highlight = pageBounds;
        nint handle = 0;
        if (!liveWindowDragEnabled && previews.TryHitTestContentDrag(localX, localY, out handle, out Rect bounds))
        {
            highlight = new(left + bounds.X * animator.Scale, top + bounds.Y * animator.Scale, bounds.Width * animator.Scale, bounds.Height * animator.Scale);
        }

        target = new(page, handle);
        ContentDragOutline.Margin = new(highlight.X, highlight.Y, 0, 0);
        ContentDragOutline.Width = highlight.Width;
        ContentDragOutline.Height = highlight.Height;
        ContentDragOutline.Visibility = Visibility.Visible;
        return true;
    }

    internal void CompleteContentDragSelection()
    {
        contentDragScrollSuppression?.Dispose();
        contentDragScrollSuppression = scrollInputSuppression.Suppress();
        scroller.CancelNavigation();
        SetInteractionEnabled(false);
    }

}
