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
using Microsoft.UI.Xaml.Controls.Primitives;

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
    private double dragDistance;

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

                Splitter grip = new() { Orientation = DesktopSharedBoundaryCalculator.IsVertical(edge) ? Orientation.Vertical : Orientation.Horizontal, RequestedTheme = ElementTheme.Dark, Scale = new((float)(1 / scale), (float)(1 / scale), 1) };
                string label = localizer.GetText("DesktopSharedBoundaryResize");
                AutomationProperties.SetName(grip, label);
                Boundary boundary = new(grip, group);
                grip.Tag = boundary;
                grip.DragStarted += HandleDragStarted;
                grip.DragDelta += HandleDragDelta;
                grip.DragCompleted += HandleDragCompleted;
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

        DesktopSnapPlacement bounds = DesktopSharedBoundaryVisual.GetBounds(boundary.Members, DesktopSharedBoundaryVisual.Thickness / scale, delta);
        boundary.Handle.Width = bounds.Width * scale;
        boundary.Handle.Height = bounds.Height * scale;
        Canvas.SetLeft(boundary.Handle, preview.VisualX + bounds.CanvasX - first.Bounds.CanvasX);
        Canvas.SetTop(boundary.Handle, preview.VisualY + bounds.CanvasY - first.Bounds.CanvasY);
    }

    private void HandleDragStarted(object sender, DragStartedEventArgs args)
    {
        if (sender is not Splitter grip)
        {
            return;
        }

        if (!enabled || active is not null || grip.Tag is not Boundary boundary)
        {
            grip.CancelDrag();
            return;
        }

        List<(DesktopSharedBoundaryMember Member, WindowResizeLimits Limits)> items = [];
        foreach (DesktopSharedBoundaryMember member in boundary.Members)
        {
            if (!windows.TryGet(member.Handle, out TrackedWindow? window) || !resizer.TryGetLimits(member.Handle, out WindowResizeLimits limits))
            {
                grip.CancelDrag();
                return;
            }

            items.Add((member with { Bounds = new(window.CanvasX, window.CanvasY, window.Width, window.Height) }, limits));
        }

        active = boundary;
        plan = new(items);
        dragDistance = 0;
        suppression = scrollSuppression.Suppress();
        scroller.CancelNavigation();
        foreach (Boundary other in boundaries)
        {
            other.Handle.Visibility = ReferenceEquals(other, boundary) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void HandleDragDelta(object sender, DragDeltaEventArgs args)
    {
        if (active is null || plan is null || !ReferenceEquals(sender, active.Handle))
        {
            return;
        }

        dragDistance += active.Handle.Orientation == Orientation.Vertical ? args.HorizontalChange : args.VerticalChange;
        plan.Update(dragDistance, scale);
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

    private void HandleDragCompleted(object sender, DragCompletedEventArgs args)
    {
        if (active is null || !ReferenceEquals(sender, active.Handle))
        {
            return;
        }

        Finish(!args.Canceled);
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
            boundary?.Handle.CancelDrag();
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

    private void Unsubscribe(Splitter grip)
    {
        grip.DragStarted -= HandleDragStarted;
        grip.DragDelta -= HandleDragDelta;
        grip.DragCompleted -= HandleDragCompleted;
        grip.Tag = null;
    }

    private sealed record Boundary(Splitter Handle, IReadOnlyList<DesktopSharedBoundaryMember> Members);
}
