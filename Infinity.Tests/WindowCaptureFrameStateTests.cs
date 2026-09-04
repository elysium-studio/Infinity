using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class WindowCaptureFrameStateTests
{
    [Fact]
    public void NewCaptureIsHiddenUntilAFrameHasBeenPresented()
    {
        WindowCaptureFrameState state = new();
        Assert.False(state.HasCurrentFrame);
        Assert.True(state.TryMarkPresented(state.Generation));
        Assert.True(state.HasCurrentFrame);
        Assert.False(state.TryMarkPresented(state.Generation));
    }

    [Fact]
    public void ReopeningCannotReuseThePreviousSessionsImage()
    {
        WindowCaptureFrameState state = new();
        long first = state.Generation;
        state.TryMarkPresented(first);
        state.Invalidate(); // close
        Assert.False(state.HasCurrentFrame);
        state.Invalidate(); // reopen before queued close has completed
        Assert.False(state.TryMarkPresented(first));
        Assert.False(state.HasCurrentFrame);
        Assert.True(state.TryMarkPresented(state.Generation));
        Assert.True(state.HasCurrentFrame);
    }

    [Fact]
    public void AFrameFinishingAfterInvalidationCannotRevealAnOldImage()
    {
        WindowCaptureFrameState state = new();
        long renderingGeneration = state.Generation;
        state.Invalidate();
        Assert.False(state.TryMarkPresented(renderingGeneration));
        Assert.False(state.HasCurrentFrame);
    }

    [Fact]
    public void LateOldSessionCompletionDoesNotInvalidateAFreshFrame()
    {
        WindowCaptureFrameState state = new();
        long old = state.Generation;
        state.Invalidate();
        state.TryMarkPresented(state.Generation);
        Assert.False(state.TryMarkPresented(old));
        Assert.True(state.HasCurrentFrame);
    }
}
