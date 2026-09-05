using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.UI.WinUI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverlayBoundaryResizeController(IWindowStore windows, IWorkspace workspace, IPager pager, IScroller scroller, IWindowBoundaryResizer resizer, IScrollInputSuppression scrollSuppression, DesktopWindowPreviewCollection previews, DesktopWindowPlacementCoordinator placement, ITextLocalizer localizer, ILogger<DesktopOverlayBoundaryResizeController> logger)
{
    private readonly List<Boundary> boundaries = [];
    private IReadOnlyList<(nint Handle, DesktopSnapPlacement Bounds)> snapshot = [];
    private Canvas? host;
    private double scale;
    private bool enabled;
    private Boundary? active;
    private DesktopSharedBoundaryResizePlan? plan;
    private IDisposable? suppression;
    private Point pointerStart;
    private uint pointerId;

    public bool IsResizing => plan is not null;

    public event Action? Completed;

    public void Attach(Canvas canvas, double overviewScale)
    {
        host = canvas;
        canvas.Translation = new(0, 0, 256);
        scale = overviewScale;
    }

    public void SetEnabled(bool value)
    {
        enabled = value;
        if (!value)
        {
            Cancel();
        }

        Refresh();
    }

    public void Refresh()
    {
        if (host is null)
        {
            return;
        }

        if (plan is not null)
        {
            if (!IsPlanValid())
            {
                Cancel();
            }

            return;
        }

        bool visible = enabled && pager.IsPageCentered(pager.CurrentPage) && workspace.Width > 0;
        host.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible)
        {
            return;
        }

        List<(nint Handle, DesktopSnapPlacement Bounds)> current = [];
        foreach (TrackedWindow window in windows)
        {
            int page = (int)Math.Floor((window.CanvasX - (double)workspace.WorkAreaX + window.Width / 2d) / workspace.Width);
            if (page != pager.CurrentPage || !previews.TryGet(window.Handle, out DesktopWindowPreview? preview) || preview is null || preview.Host.Opacity == 0)
            {
                continue;
            }

            if (preview.IsDragging)
            {
                host.Visibility = Visibility.Collapsed;
                return;
            }

            current.Add((window.Handle, new(window.CanvasX + preview.SourceOffsetX, window.CanvasY + preview.SourceOffsetY, preview.SourceWidth, preview.SourceHeight)));
        }

        if (!current.SequenceEqual(snapshot))
        {
            snapshot = current;
            Rebuild();
        }

        foreach (Boundary boundary in boundaries)
        {
            Position(boundary, 0);
        }
    }

    public void Cancel() => Finish(false);

    private void Rebuild()
    {
        if (host is null)
        {
            return;
        }

        foreach (Boundary boundary in boundaries)
        {
            Unsubscribe(boundary.Handle);
        }

        boundaries.Clear();
        host.Children.Clear();
        HashSet<string> added = [];
        foreach ((nint handle, _) in snapshot)
        {
            foreach (DesktopResizeEdge edge in new[] { DesktopResizeEdge.Right, DesktopResizeEdge.Bottom })
            {
                IReadOnlyList<DesktopSharedBoundaryMember> group = DesktopSharedBoundaryCalculator.FindGroup(handle, edge, snapshot);
                if (group.Count < 2 || !added.Add(string.Join(";", group.OrderBy(member => member.Handle).Select(member => $"{member.Handle}:{member.Edge}"))))
                {
                    continue;
                }

                SharedBoundaryHandle grip = new() { IsVertical = DesktopSharedBoundaryCalculator.IsVertical(edge), Scale = new((float)(1 / scale), (float)(1 / scale), 1) };
                string label = localizer.GetText("DesktopSharedBoundaryResize");
                AutomationProperties.SetName(grip, label);
                ToolTipService.SetToolTip(grip, label);
                Boundary boundary = new(grip, group);
                grip.Tag = boundary;
                grip.PointerPressed += HandlePressed;
                grip.PointerMoved += HandleMoved;
                grip.PointerReleased += HandleReleased;
                grip.PointerCanceled += HandleCancelled;
                grip.PointerCaptureLost += HandleCancelled;
                boundaries.Add(boundary);
                host.Children.Add(grip);
            }
        }
    }

    private void Position(Boundary boundary, double delta)
    {
        DesktopSharedBoundaryMember first = boundary.Members[0];
        if (!previews.TryGet(first.Handle, out DesktopWindowPreview? preview) || preview is null)
        {
            return;
        }

        bool vertical = boundary.Handle.IsVertical;
        double start = boundary.Members.Min(member => vertical ? member.Bounds.CanvasY : member.Bounds.CanvasX);
        double end = boundary.Members.Max(member => vertical ? member.Bounds.CanvasY + member.Bounds.Height : member.Bounds.CanvasX + member.Bounds.Width);
        double line = DesktopSharedBoundaryCalculator.Coordinate(first.Bounds, first.Edge) + delta;
        double x = preview.VisualX + (vertical ? line : (start + end) / 2) - first.Bounds.CanvasX;
        double y = preview.VisualY + (vertical ? (start + end) / 2 : line) - first.Bounds.CanvasY;
        Canvas.SetLeft(boundary.Handle, x - 16 / scale);
        Canvas.SetTop(boundary.Handle, y - 16 / scale);
    }

    private void HandlePressed(object sender, PointerRoutedEventArgs args)
    {
        if (!enabled || active is not null || sender is not SharedBoundaryHandle { Tag: Boundary boundary } grip || !args.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
        {
            return;
        }

        args.Handled = true;
        List<(DesktopSharedBoundaryMember Member, WindowResizeLimits Limits)> items = [];
        foreach (DesktopSharedBoundaryMember member in boundary.Members)
        {
            if (!windows.TryGet(member.Handle, out TrackedWindow? window) || !resizer.TryGetLimits(member.Handle, out WindowResizeLimits limits))
            {
                return;
            }

            items.Add((member with { Bounds = new(window.CanvasX, window.CanvasY, window.Width, window.Height) }, limits));
        }

        if (!grip.CapturePointer(args.Pointer))
        {
            return;
        }

        active = boundary;
        plan = new(items);
        pointerId = args.Pointer.PointerId;
        pointerStart = args.GetCurrentPoint((UIElement)grip.XamlRoot.Content).Position;
        suppression = scrollSuppression.Suppress();
        scroller.CancelNavigation();
        foreach (Boundary other in boundaries)
        {
            other.Handle.Visibility = ReferenceEquals(other, boundary) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void HandleMoved(object sender, PointerRoutedEventArgs args)
    {
        if (active is null || plan is null || args.Pointer.PointerId != pointerId)
        {
            return;
        }

        args.Handled = true;
        Point point = args.GetCurrentPoint((UIElement)active.Handle.XamlRoot.Content).Position;
        plan.Update(active.Handle.IsVertical ? point.X - pointerStart.X : point.Y - pointerStart.Y, scale);
        foreach (DesktopSharedBoundaryMember member in plan.Members)
        {
            if (!previews.TryGet(member.Handle, out DesktopWindowPreview? preview) || preview is null)
            {
                Cancel();
                return;
            }

            DesktopSnapPlacement target = plan.GetPlacement(member);
            preview.SetBoundaryResizePreview(target.CanvasX - member.Bounds.CanvasX, target.CanvasY - member.Bounds.CanvasY, preview.SourceWidth + target.Width - member.Bounds.Width, preview.SourceHeight + target.Height - member.Bounds.Height);
        }

        Position(active, plan.Delta);
    }

    private void HandleReleased(object sender, PointerRoutedEventArgs args)
    {
        if (active is null || args.Pointer.PointerId != pointerId)
        {
            return;
        }

        HandleMoved(sender, args);
        Finish(true);
        args.Handled = true;
    }

    private void HandleCancelled(object sender, PointerRoutedEventArgs args)
    {
        if (active is not null && args.Pointer.PointerId == pointerId)
        {
            args.Handled = true;
            Cancel();
        }
    }

    private bool IsPlanValid() => plan is not null && plan.Members.All(member => windows.TryGet(member.Handle, out TrackedWindow? window) && new DesktopSnapPlacement(window.CanvasX, window.CanvasY, window.Width, window.Height) == member.Bounds);

    private void Finish(bool commit)
    {
        if (plan is null)
        {
            return;
        }

        DesktopSharedBoundaryResizePlan current = plan;
        Boundary? boundary = active;
        bool apply = commit && current.Delta != 0 && IsPlanValid();
        plan = null;
        active = null;
        try
        {
            boundary?.Handle.ReleasePointerCaptures();
            if (apply)
            {
                List<(TrackedWindow Window, DesktopSnapPlacement Placement)> targets = [];
                foreach (DesktopSharedBoundaryMember member in current.Members)
                {
                    if (windows.TryGet(member.Handle, out TrackedWindow? window))
                    {
                        targets.Add((window, current.GetPlacement(member)));
                    }
                }

                placement.ApplyPlacements(targets);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not apply shared thumbnail boundary resize");
        }
        finally
        {
            foreach (DesktopSharedBoundaryMember member in current.Members)
            {
                if (previews.TryGet(member.Handle, out DesktopWindowPreview? preview))
                {
                    preview?.ClearBoundaryResizePreview();
                }
            }

            suppression?.Dispose();
            suppression = null;
            foreach (Boundary other in boundaries)
            {
                other.Handle.Visibility = Visibility.Visible;
            }

            Completed?.Invoke();
            Refresh();
        }
    }

    private void Unsubscribe(SharedBoundaryHandle grip)
    {
        grip.PointerPressed -= HandlePressed;
        grip.PointerMoved -= HandleMoved;
        grip.PointerReleased -= HandleReleased;
        grip.PointerCanceled -= HandleCancelled;
        grip.PointerCaptureLost -= HandleCancelled;
        grip.Tag = null;
    }

    private sealed record Boundary(SharedBoundaryHandle Handle, IReadOnlyList<DesktopSharedBoundaryMember> Members);
}
