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
    private nint liveWindowDragHandle;
    private DesktopWindowDragAnchor liveWindowDragAnchor;
    private DesktopWindowPreview? liveWindowDragPreview;
    private DesktopSnapPlacement? liveWindowThrowOrigin;
    private int liveWindowPointerX;
    private int liveWindowPointerY;
    private bool contentDragInside;
    private bool interactionReady;
    private Point contentDragPoint;
    private IDisposable? contentDragScrollSuppression;

    internal bool IsContentDragReady => contentDragEnabled && interactionReady && contentDragInside;

    internal DesktopWindowDragFrames DragFrames { get; }

    internal ContentDragKind DraggedContentKind { get; private set; }

    internal void BeginLiveWindowDrag(nint handle, DesktopWindowDragAnchor anchor, int screenX, int screenY, DesktopSnapPlacement? throwOrigin)
    {
        EndLiveWindowDrag();
        liveWindowDragHandle = handle;
        liveWindowDragAnchor = anchor;
        liveWindowThrowOrigin = throwOrigin;
        SetContentDragEnabled(true);
        liveWindowDragEnabled = true;
        ContentDragSurface.AllowDrop = false;
        UpdateLiveWindowDragPosition(screenX, screenY);
    }

    internal void EndLiveWindowDrag()
    {
        StopLiveWindowDragScroll();
        liveWindowDragPreview?.ClearDragVisual();
        if (liveWindowDragHandle != 0)
        {
            refreshQueue.RequestWindow(liveWindowDragHandle);
        }

        liveWindowDragPreview = null;
        liveWindowDragHandle = 0;
        liveWindowDragEnabled = false;
        ContentDragSurface.AllowDrop = true;
        SetContentDragEnabled(false);
    }

    internal void UpdateLiveWindowDragPosition(int screenX, int screenY)
    {
        liveWindowPointerX = screenX;
        liveWindowPointerY = screenY;
        UpdateLiveWindowDragPreview();
    }

    internal void UpdateLiveWindowDragScroll()
    {
        if (liveWindowDragEnabled && interactionReady)
        {
            liveWindowDragPreview?.UpdateDragScroll(contentDragPoint);
        }
        else
        {
            StopLiveWindowDragScroll();
        }
    }

    internal void StopLiveWindowDragScroll() => liveWindowDragPreview?.StopDragScroll();

    private void UpdateLiveWindowDragPreview()
    {
        if (!liveWindowDragEnabled)
        {
            return;
        }

        (double rootX, double rootY) = DragFrames.Viewport.FromScreen(liveWindowPointerX, liveWindowPointerY);
        contentDragPoint = new(rootX, rootY);
        if (!isRunning || !previews.TryGet(liveWindowDragHandle, out DesktopWindowPreview? window) || window is null)
        {
            return;
        }

        if (!ReferenceEquals(liveWindowDragPreview, window))
        {
            StopLiveWindowDragScroll();
            liveWindowDragPreview?.ClearDragVisual();
            liveWindowDragPreview = window;
        }

        window.SetLiveDragAnchor(contentDragPoint, liveWindowDragAnchor, followAnimatedScale: !interactionReady);
        if (liveWindowThrowOrigin is DesktopSnapPlacement origin && workspace.Width > 0)
        {
            int sourcePage = Math.Max(0, (int)Math.Floor((origin.CanvasX + origin.Width / 2 - workspace.WorkAreaX) / workspace.Width));
            window.SetThrowOrigin(sourcePage, origin);
        }
    }

    internal bool TryGetLiveWindowDragTarget(int screenX, int screenY, out DesktopContentDragTarget target)
    {
        UpdateLiveWindowDragPosition(screenX, screenY);
        contentDragInside = liveWindowDragEnabled;
        return TryGetContentDragTarget(out target);
    }

    internal int ReleaseLiveWindowThrowGesture() => liveWindowDragPreview?.ReleaseThrowGesture() ?? 0;

    internal bool TryCompleteLiveWindowDrop(int throwDirection, out int page)
    {
        page = -1;
        if (!liveWindowDragEnabled || !IsContentDragReady || liveWindowDragPreview is null)
        {
            return false;
        }

        if (throwDirection != 0)
        {
            return liveWindowDragPreview.TryThrowLiveWindow(throwDirection, out page);
        }

        if (!pageStrip.TryHitTestContentDrag(contentDragPoint, out int targetPage, out Rect pageBounds))
        {
            return false;
        }

        double localX = (contentDragPoint.X - pageBounds.X) / animator.Scale;
        double localY = (contentDragPoint.Y - pageBounds.Y) / animator.Scale;
        if (!liveWindowDragPreview.TryPlaceLiveDragOnPage(targetPage, localX, localY, liveWindowDragAnchor))
        {
            return false;
        }

        page = targetPage;
        return true;
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
        StopLiveWindowDragScroll();
        contentDragScrollSuppression?.Dispose();
        contentDragScrollSuppression = scrollInputSuppression.Suppress();
        scroller.CancelNavigation();
        SetInteractionEnabled(false);
    }

}
