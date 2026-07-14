using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public class DesktopNavigationHistory(IWindowStore store,
    IPager pager,
    IScroller scroller,
    IWindowNavigationCoordinator navigationCoordinator,
    IForegroundWindowTracker foregroundWindowTracker,
    IForegroundWindowSource foregroundWindowSource,
    IWorkspace workspace,
    IDispatcher dispatcher,
    DesktopHistoryConfiguration configuration,
    ILogger<DesktopNavigationHistory> logger) :
    IDesktopNavigationHistory,
    IDesktopNavigationHistoryLifetime
{
    private static readonly TimeSpan PageCommitDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReplayTimeout = TimeSpan.FromSeconds(2);

    private readonly Lock syncRoot = new();
    private readonly List<HistoryLocation> back = [];
    private readonly List<HistoryLocation> forward = [];

    private CancellationTokenSource? pageCommitCancellationTokenSource;
    private CancellationTokenSource? replayCancellationTokenSource;
    private HistoryLocation? current;
    private HistoryLocation? replayTarget;
    private int pendingPage = -1;
    private int capacity = DesktopHistoryDefaults.Capacity;
    private long nextId;
    private long pageCommitGeneration;
    private long replayGeneration;
    private bool enabled = true;
    private bool isStarted;

    public event EventHandler? Changed;

    public bool IsEnabled
    {
        get
        {
            lock (syncRoot)
            {
                return enabled;
            }
        }
    }

    public bool CanGoBack
    {
        get
        {
            lock (syncRoot)
            {
                return enabled && back.Any(location => IsValidCore(location) && !IsEquivalent(location, current));
            }
        }
    }

    public bool CanGoForward
    {
        get
        {
            lock (syncRoot)
            {
                return enabled && forward.Any(location => IsValidCore(location) && !IsEquivalent(location, current));
            }
        }
    }

    public IReadOnlyList<DesktopHistoryEntry> BackEntries
    {
        get
        {
            lock (syncRoot)
            {
                return back
                    .Where(IsValidCore)
                    .AsEnumerable()
                    .Reverse()
                    .Select(location => location.Entry)
                    .ToArray();
            }
        }
    }

    public IReadOnlyList<DesktopHistoryEntry> ForwardEntries
    {
        get
        {
            lock (syncRoot)
            {
                return forward
                    .Where(IsValidCore)
                    .AsEnumerable()
                    .Reverse()
                    .Select(location => location.Entry)
                    .ToArray();
            }
        }
    }

    public void Start()
    {
        bool changed;

        lock (syncRoot)
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
            DesktopHistoryConfigurationSnapshot snapshot = configuration.Current;
            enabled = snapshot.Enabled;
            capacity = snapshot.Capacity;
            current = enabled ? CreateCurrentLocationCore() : null;
            changed = enabled;
        }

        configuration.Changed += HandleConfigurationChanged;
        foregroundWindowTracker.ForegroundWindowChanged += HandleForegroundWindowChanged;
        pager.PageChanged += HandlePageChanged;
        scroller.ScrollStopped += HandleScrollStopped;
        store.WindowAdded += HandleWindowAdded;
        store.WindowRemoved += HandleWindowRemoved;
        store.WindowChanged += HandleWindowChanged;

        if (changed)
        {
            PublishChanged();
        }
    }

    public void Stop()
    {
        lock (syncRoot)
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            CancelPageCommitCore();
            CancelReplayCore();
            pendingPage = -1;
        }

        configuration.Changed -= HandleConfigurationChanged;
        foregroundWindowTracker.ForegroundWindowChanged -= HandleForegroundWindowChanged;
        pager.PageChanged -= HandlePageChanged;
        scroller.ScrollStopped -= HandleScrollStopped;
        store.WindowAdded -= HandleWindowAdded;
        store.WindowRemoved -= HandleWindowRemoved;
        store.WindowChanged -= HandleWindowChanged;
    }

    public bool GoBack() => Traverse(back, forward);

    public bool GoForward() => Traverse(forward, back);

    public bool NavigateTo(long entryId)
    {
        HistoryLocation? destination = null;
        bool changed;

        lock (syncRoot)
        {
            if (!isStarted || !enabled)
            {
                return false;
            }

            changed = PruneInvalidCore();
            int index = back.FindIndex(location => location.Entry.Id == entryId);

            if (index >= 0)
            {
                destination = TraverseToIndexCore(back, forward, index);
            }
            else
            {
                index = forward.FindIndex(location => location.Entry.Id == entryId);

                if (index >= 0)
                {
                    destination = TraverseToIndexCore(forward, back, index);
                }
            }

            if (destination is not null)
            {
                BeginReplayCore(destination);
                changed = true;
            }
        }

        if (changed)
        {
            PublishChanged();
        }

        if (destination is null)
        {
            return false;
        }

        Navigate(destination);
        return true;
    }

    public void Clear()
    {
        bool changed;

        lock (syncRoot)
        {
            changed = back.Count > 0 || forward.Count > 0 || replayTarget is not null;
            back.Clear();
            forward.Clear();
            CancelReplayCore();
            current = enabled && isStarted ? CreateCurrentLocationCore() : null;
        }

        if (changed)
        {
            PublishChanged();
        }
    }

    private bool Traverse(List<HistoryLocation> source, List<HistoryLocation> destinationStack)
    {
        HistoryLocation? destination = null;

        lock (syncRoot)
        {
            if (!isStarted || !enabled)
            {
                return false;
            }

            PruneInvalidCore();

            while (source.Count > 0)
            {
                int index = source.Count - 1;
                HistoryLocation candidate = source[index];
                source.RemoveAt(index);

                if (!IsValidCore(candidate) || IsEquivalent(candidate, current))
                {
                    continue;
                }

                if (current is not null)
                {
                    destinationStack.Add(current);
                }

                current = candidate;
                destination = candidate;
                TrimToCapacityCore();
                BeginReplayCore(candidate);
                break;
            }
        }

        if (destination is null)
        {
            PublishChanged();
            return false;
        }

        PublishChanged();
        Navigate(destination);
        return true;
    }

    private HistoryLocation? TraverseToIndexCore(List<HistoryLocation> source,
        List<HistoryLocation> destinationStack,
        int targetIndex)
    {
        HistoryLocation? destination = null;

        while (source.Count > targetIndex)
        {
            int index = source.Count - 1;
            HistoryLocation candidate = source[index];
            source.RemoveAt(index);

            if (!IsValidCore(candidate) || IsEquivalent(candidate, current))
            {
                continue;
            }

            if (current is not null)
            {
                destinationStack.Add(current);
            }

            current = candidate;
            destination = candidate;
        }

        TrimToCapacityCore();
        return destination;
    }

    private void Navigate(HistoryLocation destination)
    {
        if (destination.Window is null)
        {
            pager.NavigateToPage(destination.Entry.Page);
            return;
        }

        if (destination.Window.IsSticky)
        {
            pager.NavigateToPage(destination.Entry.Page);
            navigationCoordinator.Activate(destination.Entry.WindowHandle);
            return;
        }

        navigationCoordinator.NavigateTo(destination.Entry.WindowHandle);
    }

    private void HandleConfigurationChanged(DesktopHistoryConfigurationSnapshot snapshot)
    {
        bool changed = false;

        lock (syncRoot)
        {
            if (!isStarted)
            {
                enabled = snapshot.Enabled;
                capacity = snapshot.Capacity;
                return;
            }

            if (enabled != snapshot.Enabled)
            {
                enabled = snapshot.Enabled;
                back.Clear();
                forward.Clear();
                CancelPageCommitCore();
                CancelReplayCore();
                pendingPage = -1;
                current = enabled ? CreateCurrentLocationCore() : null;
                changed = true;
            }

            if (capacity != snapshot.Capacity)
            {
                capacity = snapshot.Capacity;
                changed |= TrimToCapacityCore();
            }

            changed |= PruneInvalidCore();

            if (current is not null && !IsValidCore(current))
            {
                current = CreateCurrentLocationCore();
                changed = true;
            }

            if (replayTarget is not null && !IsValidCore(replayTarget))
            {
                CancelReplayCore();
                changed = true;
            }
        }

        if (changed)
        {
            PublishChanged();
        }
    }

    private void HandleForegroundWindowChanged(object? sender, IntPtr handle)
    {
        HistoryLocation? location;
        bool shouldRecord;

        lock (syncRoot)
        {
            if (!isStarted || !enabled || handle == default || !store.TryGet(handle, out TrackedWindow trackedWindow))
            {
                return;
            }

            location = CreateWindowLocationCore(trackedWindow,
                trackedWindow.IsSticky ? pager.CurrentPage : GetWindowPage(trackedWindow));

            if (replayTarget is not null && IsSameWindow(replayTarget, location))
            {
                current = PreserveIdentity(replayTarget, location);
                CancelReplayCore();
                shouldRecord = false;
            }
            else if (replayTarget is not null)
            {
                return;
            }
            else
            {
                shouldRecord = true;
            }
        }

        if (shouldRecord)
        {
            Record(location);
        }
        else
        {
            PublishChanged();
        }
    }

    private void HandlePageChanged(int page)
    {
        CancellationTokenSource cancellationTokenSource = new();
        long generation;

        lock (syncRoot)
        {
            if (!isStarted || !enabled)
            {
                cancellationTokenSource.Dispose();
                return;
            }

            CancelPageCommitCore();
            pendingPage = page;
            pageCommitGeneration++;
            generation = pageCommitGeneration;
            pageCommitCancellationTokenSource = cancellationTokenSource;
        }

        _ = DelayAndCommitPageAsync(generation, page, cancellationTokenSource);
    }

    private void HandleScrollStopped(object? sender, EventArgs args)
    {
        int page;

        lock (syncRoot)
        {
            if (!isStarted || !enabled || pendingPage < 0)
            {
                return;
            }

            page = pendingPage;
            pendingPage = -1;
            CancelPageCommitCore();
        }

        CommitPage(page);
    }

    private async Task DelayAndCommitPageAsync(long generation,
        int page,
        CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await Task.Delay(PageCommitDelay, cancellationTokenSource.Token).ConfigureAwait(false);
            dispatcher.Dispatch(() => CommitDelayedPage(generation, page, cancellationTokenSource));
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to commit desktop history page {Page}", page);
        }
        finally
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(pageCommitCancellationTokenSource, cancellationTokenSource))
                {
                    pageCommitCancellationTokenSource = null;
                }
            }

            cancellationTokenSource.Dispose();
        }
    }

    private void CommitDelayedPage(long generation,
        int page,
        CancellationTokenSource cancellationTokenSource)
    {
        lock (syncRoot)
        {
            if (!isStarted || !enabled || generation != pageCommitGeneration ||
                !ReferenceEquals(pageCommitCancellationTokenSource, cancellationTokenSource))
            {
                return;
            }

            pageCommitCancellationTokenSource = null;
            pendingPage = -1;
        }

        CommitPage(page);
    }

    private void CommitPage(int page)
    {
        HistoryLocation? location = null;
        bool replayCompleted = false;

        lock (syncRoot)
        {
            if (!isStarted || !enabled || !IsValidPageCore(page))
            {
                return;
            }

            if (replayTarget is not null)
            {
                if (replayTarget.Entry.Page == page)
                {
                    if (replayTarget.Window is null || replayTarget.Window.IsSticky)
                    {
                        current = replayTarget;
                        CancelReplayCore();
                        replayCompleted = true;
                    }
                }
                else
                {
                    CancelReplayCore();
                }
            }

            bool shouldRecord = !replayCompleted && replayTarget is null &&
                navigationCoordinator.PageBeforeFilter < 0 && navigationCoordinator.PendingActivation == default;

            if (shouldRecord)
            {
                if (current?.Window is TrackedWindow trackedWindow && IsCurrentWindowCore(current))
                {
                    int windowPage = trackedWindow.IsSticky ? page : GetWindowPage(trackedWindow);

                    if (trackedWindow.IsSticky || windowPage == page)
                    {
                        location = CreateWindowLocationCore(trackedWindow, page);
                    }
                }

                location ??= CreatePageLocationCore(page);
            }
        }

        if (replayCompleted)
        {
            PublishChanged();
        }
        else if (location is not null)
        {
            Record(location);
        }
    }

    private void Record(HistoryLocation location)
    {
        bool changed;

        lock (syncRoot)
        {
            if (!isStarted || !enabled || !IsValidCore(location))
            {
                return;
            }

            CancelReplayCore();

            if (current is null)
            {
                current = location;
                changed = true;
            }
            else if (IsEquivalent(current, location))
            {
                HistoryLocation updated = PreserveIdentity(current, location);
                changed = updated.Entry.WindowTitle != current.Entry.WindowTitle ||
                    updated.Entry.Page != current.Entry.Page;
                current = updated;
            }
            else
            {
                back.Add(current);
                current = location;
                forward.Clear();
                TrimToCapacityCore();
                changed = true;
            }
        }

        if (changed)
        {
            PublishChanged();
        }
    }

    private void HandleWindowAdded(object? sender, TrackedWindow trackedWindow)
    {
        bool changed;

        lock (syncRoot)
        {
            changed = RemoveHandleCore(trackedWindow.Handle, trackedWindow);
        }

        if (changed)
        {
            PublishChanged();
        }
    }

    private void HandleWindowRemoved(object? sender, IntPtr handle)
    {
        bool changed;

        lock (syncRoot)
        {
            changed = RemoveHandleCore(handle, null);

            if (current?.Entry.WindowHandle == handle)
            {
                current = CreatePageLocationCore(pager.CurrentPage);
                changed = true;
            }

            if (replayTarget?.Entry.WindowHandle == handle)
            {
                CancelReplayCore();
                changed = true;
            }
        }

        if (changed)
        {
            PublishChanged();
        }
    }

    private void HandleWindowChanged(object? sender, TrackedWindow trackedWindow)
    {
        bool changed = false;

        lock (syncRoot)
        {
            changed |= RemoveHandleCore(trackedWindow.Handle, trackedWindow);

            if (current?.Entry.WindowHandle == trackedWindow.Handle && !ReferenceEquals(current.Window, trackedWindow))
            {
                current = CreatePageLocationCore(pager.CurrentPage);
                changed = true;
            }

            if (replayTarget?.Entry.WindowHandle == trackedWindow.Handle && !ReferenceEquals(replayTarget.Window, trackedWindow))
            {
                CancelReplayCore();
                changed = true;
            }

            changed |= UpdateWindowEntriesCore(back, trackedWindow);
            changed |= UpdateWindowEntriesCore(forward, trackedWindow);

            if (current is not null && ReferenceEquals(current.Window, trackedWindow))
            {
                HistoryLocation updated = UpdateWindowLocationCore(current, trackedWindow);
                changed |= updated.Entry != current.Entry;
                current = updated;
            }

            if (replayTarget is not null && ReferenceEquals(replayTarget.Window, trackedWindow))
            {
                replayTarget = UpdateWindowLocationCore(replayTarget, trackedWindow);
            }
        }

        if (changed)
        {
            PublishChanged();
        }
    }

    private bool UpdateWindowEntriesCore(List<HistoryLocation> locations, TrackedWindow trackedWindow)
    {
        bool changed = false;

        for (int index = 0; index < locations.Count; index++)
        {
            HistoryLocation location = locations[index];

            if (!ReferenceEquals(location.Window, trackedWindow))
            {
                continue;
            }

            HistoryLocation updated = UpdateWindowLocationCore(location, trackedWindow);
            changed |= updated.Entry != location.Entry;
            locations[index] = updated;
        }

        return changed;
    }

    private HistoryLocation UpdateWindowLocationCore(HistoryLocation location, TrackedWindow trackedWindow)
    {
        int page = trackedWindow.IsSticky ? location.Entry.Page : GetWindowPage(trackedWindow);
        DesktopHistoryEntry entry = location.Entry with
        {
            Page = page,
            WindowTitle = trackedWindow.Title
        };

        return new(entry, trackedWindow);
    }

    private bool RemoveHandleCore(IntPtr handle, TrackedWindow? except)
    {
        int backRemoved = back.RemoveAll(location => location.Entry.WindowHandle == handle && !ReferenceEquals(location.Window, except));
        int forwardRemoved = forward.RemoveAll(location => location.Entry.WindowHandle == handle && !ReferenceEquals(location.Window, except));
        return backRemoved > 0 || forwardRemoved > 0;
    }

    private bool PruneInvalidCore()
    {
        int backRemoved = back.RemoveAll(location => !IsValidCore(location));
        int forwardRemoved = forward.RemoveAll(location => !IsValidCore(location));
        return backRemoved > 0 || forwardRemoved > 0;
    }

    private bool IsValidCore(HistoryLocation location)
    {
        if (!IsValidPageCore(location.Entry.Page))
        {
            return false;
        }

        if (location.Window is null)
        {
            return true;
        }

        return store.TryGet(location.Entry.WindowHandle, out TrackedWindow trackedWindow) &&
            ReferenceEquals(trackedWindow, location.Window);
    }

    private bool IsValidPageCore(int page) =>
        page >= 0 && (pager.MaxPages is null || page < pager.MaxPages.Value);

    private bool IsCurrentWindowCore(HistoryLocation location) =>
        location.Window is not null &&
        store.TryGet(location.Entry.WindowHandle, out TrackedWindow trackedWindow) &&
        ReferenceEquals(trackedWindow, location.Window);

    private HistoryLocation CreateCurrentLocationCore()
    {
        IntPtr handle = foregroundWindowSource.GetForegroundWindow();

        if (handle != default && store.TryGet(handle, out TrackedWindow trackedWindow))
        {
            int page = trackedWindow.IsSticky ? pager.CurrentPage : GetWindowPage(trackedWindow);
            return CreateWindowLocationCore(trackedWindow, page);
        }

        return CreatePageLocationCore(pager.CurrentPage);
    }

    private HistoryLocation CreateWindowLocationCore(TrackedWindow trackedWindow, int page) =>
        new(new DesktopHistoryEntry(++nextId,
            trackedWindow.Handle,
            page,
            trackedWindow.Title,
            DateTimeOffset.Now),
            trackedWindow);

    private HistoryLocation CreatePageLocationCore(int page) =>
        new(new DesktopHistoryEntry(++nextId,
            default,
            page,
            string.Empty,
            DateTimeOffset.Now),
            null);

    private int GetWindowPage(TrackedWindow trackedWindow)
    {
        if (workspace.Width <= 0)
        {
            return 0;
        }

        long page = (long)Math.Floor(trackedWindow.CanvasX / (double)workspace.Width);
        page = Math.Max(0, page);

        if (pager.MaxPages is int maxPages)
        {
            page = Math.Min(page, maxPages - 1L);
        }

        return (int)Math.Min(page, int.MaxValue);
    }

    private static bool IsEquivalent(HistoryLocation? left, HistoryLocation? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.Window is null || right.Window is null)
        {
            return left.Window is null && right.Window is null && left.Entry.Page == right.Entry.Page;
        }

        return ReferenceEquals(left.Window, right.Window) && left.Entry.Page == right.Entry.Page;
    }

    private static bool IsSameWindow(HistoryLocation left, HistoryLocation right) =>
        left.Window is not null && right.Window is not null && ReferenceEquals(left.Window, right.Window);

    private static HistoryLocation PreserveIdentity(HistoryLocation original, HistoryLocation updated) =>
        new(updated.Entry with
        {
            Id = original.Entry.Id,
            VisitedAt = original.Entry.VisitedAt
        }, updated.Window);

    private bool TrimToCapacityCore()
    {
        bool changed = false;

        while (back.Count + forward.Count > capacity)
        {
            (List<HistoryLocation> list, int index) = FindOldestCore();
            list.RemoveAt(index);
            changed = true;
        }

        return changed;
    }

    private (List<HistoryLocation> List, int Index) FindOldestCore()
    {
        List<HistoryLocation> oldestList = back.Count > 0 ? back : forward;
        int oldestIndex = 0;
        DateTimeOffset oldest = oldestList[0].Entry.VisitedAt;

        FindOldestInList(back, ref oldestList, ref oldestIndex, ref oldest);
        FindOldestInList(forward, ref oldestList, ref oldestIndex, ref oldest);

        return (oldestList, oldestIndex);
    }

    private static void FindOldestInList(List<HistoryLocation> candidateList,
        ref List<HistoryLocation> oldestList,
        ref int oldestIndex,
        ref DateTimeOffset oldest)
    {
        for (int index = 0; index < candidateList.Count; index++)
        {
            DateTimeOffset visitedAt = candidateList[index].Entry.VisitedAt;

            if (visitedAt >= oldest)
            {
                continue;
            }

            oldest = visitedAt;
            oldestList = candidateList;
            oldestIndex = index;
        }
    }

    private void BeginReplayCore(HistoryLocation target)
    {
        CancelPageCommitCore();
        pendingPage = -1;
        CancelReplayCore();
        replayTarget = target;
        replayGeneration++;
        long generation = replayGeneration;
        CancellationTokenSource cancellationTokenSource = new();
        replayCancellationTokenSource = cancellationTokenSource;
        _ = ExpireReplayAsync(generation, cancellationTokenSource);
    }

    private async Task ExpireReplayAsync(long generation, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await Task.Delay(ReplayTimeout, cancellationTokenSource.Token).ConfigureAwait(false);
            dispatcher.Dispatch(() => ExpireReplay(generation, cancellationTokenSource));
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to expire desktop history replay");
        }
        finally
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(replayCancellationTokenSource, cancellationTokenSource))
                {
                    replayCancellationTokenSource = null;
                }
            }

            cancellationTokenSource.Dispose();
        }
    }

    private void ExpireReplay(long generation, CancellationTokenSource cancellationTokenSource)
    {
        bool shouldReconcile = false;

        lock (syncRoot)
        {
            if (generation == replayGeneration && ReferenceEquals(replayCancellationTokenSource, cancellationTokenSource))
            {
                replayCancellationTokenSource = null;
                replayTarget = null;
                shouldReconcile = isStarted && enabled;
            }
        }

        if (!shouldReconcile)
        {
            return;
        }

        HistoryLocation location;

        lock (syncRoot)
        {
            if (!isStarted || !enabled || replayTarget is not null)
            {
                return;
            }

            location = CreateCurrentLocationCore();
        }

        Record(location);
    }

    private void CancelPageCommitCore()
    {
        pageCommitGeneration++;
        CancellationTokenSource? cancellationTokenSource = pageCommitCancellationTokenSource;
        pageCommitCancellationTokenSource = null;

        if (cancellationTokenSource is not null)
        {
            TryCancel(cancellationTokenSource);
        }
    }

    private void CancelReplayCore()
    {
        replayGeneration++;
        replayTarget = null;
        CancellationTokenSource? cancellationTokenSource = replayCancellationTokenSource;
        replayCancellationTokenSource = null;

        if (cancellationTokenSource is not null)
        {
            TryCancel(cancellationTokenSource);
        }
    }

    private static void TryCancel(CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void PublishChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private record HistoryLocation(DesktopHistoryEntry Entry, TrackedWindow? Window);
}
