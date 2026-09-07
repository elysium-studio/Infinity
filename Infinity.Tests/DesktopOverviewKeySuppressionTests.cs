using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopOverviewKeySuppressionTests
{
    [Theory]
    [InlineData(0x10)]
    [InlineData(0x11)]
    [InlineData(0x12)]
    [InlineData(0x5B)]
    [InlineData(0x5C)]
    [InlineData(0xA0)]
    [InlineData(0xA1)]
    [InlineData(0xA2)]
    [InlineData(0xA3)]
    [InlineData(0xA4)]
    [InlineData(0xA5)]
    public void ModifierRepeatsDuringPeekNeverConsumeTheRelease(int key)
    {
        DesktopOverviewKeySuppression suppression = new();
        Assert.True(DesktopOverviewKeySuppression.IsModifier(key));
        suppression.Track(key);
        suppression.Track(key);
        Assert.False(suppression.Release(key));
    }

    [Fact]
    public void ConsumedOrdinaryKeyReleasesAreSuppressedOnce()
    {
        DesktopOverviewKeySuppression suppression = new();
        Assert.False(DesktopOverviewKeySuppression.IsModifier(0x41));
        suppression.Track(0x41);
        suppression.Track(0x41);
        Assert.True(suppression.Release(0x41));
        Assert.False(suppression.Release(0x41));
        Assert.False(suppression.Release(0x42));
    }

    [Fact]
    public void EmergencyClearDiscardsPendingReleases()
    {
        DesktopOverviewKeySuppression suppression = new();
        suppression.Track(0x1B);
        suppression.Clear();
        Assert.False(suppression.Release(0x1B));
    }
}
