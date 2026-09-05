using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class ScrollInputSuppressionTests
{
    [Fact]
    public void SuppressionRemainsActiveUntilEveryLeaseIsReleased()
    {
        ScrollInputSuppression suppression = new();
        Assert.False(suppression.IsSuppressed);
        IDisposable first = suppression.Suppress();
        IDisposable second = suppression.Suppress();
        Assert.True(suppression.IsSuppressed);
        first.Dispose();
        Assert.True(suppression.IsSuppressed);
        second.Dispose();
        Assert.False(suppression.IsSuppressed);
    }
}
