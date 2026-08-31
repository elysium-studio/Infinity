using System;
using System.Collections.Generic;
using System.Linq;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowGroupStackAnimator
{
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(160);

    private readonly HashSet<nint> handles = [];
    private nint leaderHandle;

    public nint LeaderHandle => leaderHandle;

    public bool IsActive => leaderHandle != 0;

    internal void Begin(nint leader, IEnumerable<nint> selectedHandles, IReadOnlyDictionary<nint, DesktopWindowPreview> previews)
    {
        leaderHandle = leader;
        handles.Clear();
        handles.UnionWith(selectedHandles);

        if (previews.TryGetValue(leader, out DesktopWindowPreview? leaderPreview))
        {
            leaderPreview.SetGroupDragLeader(true);
            Update(previews, TransitionDuration);
        }
    }

    internal void Update(IReadOnlyDictionary<nint, DesktopWindowPreview> previews, TimeSpan? transitionDuration = null)
    {
        if (leaderHandle == 0 || !previews.TryGetValue(leaderHandle, out DesktopWindowPreview? leader))
        {
            return;
        }

        DesktopWindowPreview[] followers = [.. previews
            .Where(item => item.Key != leaderHandle && handles.Contains(item.Key))
            .OrderByDescending(item => item.Value.ZIndex)
            .ThenBy(item => (long)item.Key)
            .Select(item => item.Value)];

        for (int index = 0; index < followers.Length; index++)
        {
            int depth = index + 1;
            double offset = Math.Min(depth, 6) * (8 / leader.LayoutScale);
            float scale = Math.Max(0.86f, 1 - (Math.Min(depth, 6) * 0.025f));
            followers[index].SetGroupStackTarget(leader.VisualX + offset, leader.VisualY + offset, scale, depth, transitionDuration);
        }
    }

    internal void End(IReadOnlyDictionary<nint, DesktopWindowPreview> previews)
    {
        foreach (nint handle in handles)
        {
            if (previews.TryGetValue(handle, out DesktopWindowPreview? preview))
            {
                preview.ClearGroupDragVisual(TransitionDuration);
            }
        }

        Reset();
    }

    public void Remove(nint handle)
    {
        handles.Remove(handle);

        if (leaderHandle == handle)
        {
            Reset();
        }
    }

    public void Reset()
    {
        handles.Clear();
        leaderHandle = 0;
    }
}
