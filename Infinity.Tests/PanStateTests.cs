using Infinity.Application;

namespace Infinity.Tests;

public sealed class PanStateTests
{
    [Fact]
    public void ApplyDeltaClampsOffsetToConfiguredBounds()
    {
        PanState state = new();
        state.SetMaxOffset(100);
        state.SetOffset(50);

        state.ApplyDelta(75);
        Assert.Equal(100, state.Offset);

        state.ApplyDelta(-150);
        Assert.Equal(0, state.Offset);
    }

    [Fact]
    public void OffsetChangesRaiseOneNotificationPerOperation()
    {
        PanState state = new();
        int notifications = 0;
        state.OffsetChanged += () => notifications++;

        state.SetOffset(25);
        state.ApplyDelta(10);

        Assert.Equal(35, state.Offset);
        Assert.Equal(2, notifications);
    }
}
