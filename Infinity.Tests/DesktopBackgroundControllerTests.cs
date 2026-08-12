using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopBackgroundControllerTests
{
    [Fact]
    public void PollingFollowsSubscriptionLifetime()
    {
        TestDesktopBackgroundSource source = new();
        DesktopBackgroundController controller = new(source, new TestDispatcher());

        controller.Subscribe();
        controller.Subscribe();

        Assert.Equal(1, source.StartCount);

        controller.Unsubscribe();
        controller.Unsubscribe();

        Assert.Equal(1, source.StopCount);
    }

    private sealed class TestDesktopBackgroundSource :
        IDesktopBackgroundSource
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public event EventHandler? BackgroundChanged
        {
            add { }
            remove { }
        }

        public DesktopBackground GetBackground() => new();

        public void Start() => StartCount++;

        public void Stop() => StopCount++;
    }

    private sealed class TestDispatcher :
        IDispatcher
    {
        public void Dispatch(Action action) => action();
    }
}
