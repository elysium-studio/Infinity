using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class ScrollInputSuppressionTests
{
    [Fact]
    public void KeyboardSuppressionAllowsWheelBrowsing()
    {
        ScrollInputSuppression suppression = new();
        IDisposable keyboard = suppression.SuppressKeyboard();
        Assert.True(suppression.IsSuppressed);
        Assert.False(suppression.IsWheelSuppressed);
        keyboard.Dispose();
        keyboard.Dispose();
        Assert.False(suppression.IsSuppressed);
    }

    [Fact]
    public void FullSuppressionOverridesKeyboardOnlyLeases()
    {
        ScrollInputSuppression suppression = new();
        using IDisposable keyboard = suppression.SuppressKeyboard();
        IDisposable all = suppression.Suppress();
        Assert.True(suppression.IsWheelSuppressed);
        all.Dispose();
        Assert.False(suppression.IsWheelSuppressed);
        Assert.True(suppression.IsSuppressed);
    }

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
