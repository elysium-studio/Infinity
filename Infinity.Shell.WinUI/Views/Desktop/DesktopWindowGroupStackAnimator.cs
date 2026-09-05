using System;
using System.Collections.Generic;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowGroupStackAnimator
{
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(160);
    private readonly HashSet<nint> handles = [];
    private readonly List<KeyValuePair<nint, DesktopWindowPreview>> followers = [];
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

        followers.Clear();
        foreach (nint handle in handles)
        {
            if (handle != leaderHandle && previews.TryGetValue(handle, out DesktopWindowPreview? follower))
            {
                followers.Add(new(handle, follower));
            }
        }

        followers.Sort(static (left, right) =>
        {
            int order = right.Value.ZIndex.CompareTo(left.Value.ZIndex);
            return order != 0 ? order : ((long)left.Key).CompareTo((long)right.Key);
        });
        for (int index = 0; index < followers.Count; index++)
        {
            int depth = index + 1;
            double offset = Math.Min(depth, 6) * (8 / leader.LayoutScale);
            float scale = Math.Max(0.86f, 1 - (Math.Min(depth, 6) * 0.025f));
            followers[index].Value.SetGroupStackTarget(leader.VisualX + offset, leader.VisualY + offset, scale, depth, transitionDuration);
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
        followers.Clear();
        leaderHandle = 0;
    }
}
