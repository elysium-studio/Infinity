using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopOverviewRefreshQueueTests
{
    [Fact]
    public void WindowChangesAreDeduplicatedIntoOneDispatch()
    {
        Queue<Action> dispatcher = new();
        List<DesktopOverviewRefreshBatch> batches = [];
        DesktopOverviewRefreshQueue queue = Create(dispatcher, batches.Add);
        queue.Start();
        for (int index = 0; index < 100; index++)
        {
            queue.RequestWindow(1);
            queue.RequestWindow(2);
        }

        Assert.Single(dispatcher);
        dispatcher.Dequeue()();
        DesktopOverviewRefreshBatch batch = Assert.Single(batches);
        Assert.False(batch.Synchronise);
        Assert.False(batch.Layout);
        Assert.Equal(new nint[] { 1, 2 }, batch.Windows.Order());
    }

    [Fact]
    public void SynchronisationSupersedesIndividualWindowsButPreservesLayoutRequest()
    {
        Queue<Action> dispatcher = new();
        List<DesktopOverviewRefreshBatch> batches = [];
        DesktopOverviewRefreshQueue queue = Create(dispatcher, batches.Add);
        queue.Start();
        queue.RequestWindow(1);
        queue.RequestLayout();
        queue.RequestSynchronise();
        queue.RequestWindow(2);
        dispatcher.Dequeue()();
        DesktopOverviewRefreshBatch batch = Assert.Single(batches);
        Assert.True(batch.Synchronise);
        Assert.True(batch.Layout);
        Assert.Empty(batch.Windows);
        Assert.Empty(dispatcher);
    }

    [Fact]
    public void LayoutRefreshKeepsDirtyWindowsSoTheirSourceGeometryIsUpdated()
    {
        Queue<Action> dispatcher = new();
        List<DesktopOverviewRefreshBatch> batches = [];
        DesktopOverviewRefreshQueue queue = Create(dispatcher, batches.Add);
        queue.Start();
        queue.RequestWindow(1);
        queue.RequestLayout();
        dispatcher.Dequeue()();
        DesktopOverviewRefreshBatch batch = Assert.Single(batches);
        Assert.True(batch.Layout);
        Assert.Equal(new nint[] { 1 }, batch.Windows);
    }

    [Fact]
    public void RequestsDuringRefreshRunInTheNextDispatch()
    {
        Queue<Action> dispatcher = new();
        List<DesktopOverviewRefreshBatch> batches = [];
        DesktopOverviewRefreshQueue? queue = null;
        queue = Create(dispatcher, batch =>
        {
            batches.Add(batch);
            if (batches.Count == 1)
            {
                queue!.RequestWindow(2);
            }
        });
        queue.Start();
        queue.RequestWindow(1);
        dispatcher.Dequeue()();
        Assert.Single(dispatcher);
        dispatcher.Dequeue()();
        Assert.Equal(2, batches.Count);
        Assert.Equal(new nint[] { 1 }, batches[0].Windows);
        Assert.Equal(new nint[] { 2 }, batches[1].Windows);
    }

    [Fact]
    public void ClosingDropsQueuedAndSubsequentRefreshes()
    {
        Queue<Action> dispatcher = new();
        List<DesktopOverviewRefreshBatch> batches = [];
        DesktopOverviewRefreshQueue queue = Create(dispatcher, batches.Add);
        queue.Start();
        queue.RequestSynchronise();
        queue.Stop();
        queue.RequestLayout();
        dispatcher.Dequeue()();
        Assert.Empty(batches);
        Assert.Empty(dispatcher);
    }

    [Fact]
    public void ReopeningDoesNotApplyOldWindowChangesOrLoseNewOnes()
    {
        Queue<Action> dispatcher = new();
        List<DesktopOverviewRefreshBatch> batches = [];
        DesktopOverviewRefreshQueue queue = Create(dispatcher, batches.Add);
        queue.Start();
        queue.RequestWindow(1);
        queue.Stop();
        queue.Start();
        queue.RequestWindow(2);
        Assert.Single(dispatcher);
        dispatcher.Dequeue()();
        Assert.Equal(new nint[] { 2 }, Assert.Single(batches).Windows);
    }

    [Fact]
    public void RejectedDispatchCanBeRetriedWithoutLosingDirtyWindows()
    {
        Queue<Action> dispatcher = new();
        List<DesktopOverviewRefreshBatch> batches = [];
        bool accept = false;
        DesktopOverviewRefreshQueue queue = new(action =>
        {
            if (!accept)
            {
                return false;
            }

            dispatcher.Enqueue(action);
            return true;
        }, batches.Add);
        queue.Start();
        queue.RequestWindow(1);
        accept = true;
        queue.RequestWindow(2);
        dispatcher.Dequeue()();
        Assert.Equal(new nint[] { 1, 2 }, Assert.Single(batches).Windows.Order());
    }

    private static DesktopOverviewRefreshQueue Create(Queue<Action> dispatcher, Action<DesktopOverviewRefreshBatch> refresh) => new(action =>
    {
        dispatcher.Enqueue(action);
        return true;
    }, refresh);
}
