using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public class DesktopHistoryShortcutControllerTests
{
    [Fact]
    public void ConfiguredShortcutsNavigateAndSuppressKeyRepeat()
    {
        TestKeyboardInputSource input = new();
        TestHistory history = new();
        DesktopHistoryConfiguration configuration = new();
        DesktopHistoryShortcutController controller = new(input, history, configuration);
        controller.Start();
        input.KeysDown.UnionWith([0x5B, 0xA2]);

        KeyEventArgs first = input.RaiseDown(0xDB);
        KeyEventArgs repeated = input.RaiseDown(0xDB);
        KeyEventArgs released = input.RaiseUp(0xDB);
        input.RaiseDown(0xDB);

        Assert.True(first.Handled);
        Assert.True(repeated.Handled);
        Assert.True(released.Handled);
        Assert.Equal(2, history.BackCount);
        Assert.Equal(0, history.ForwardCount);

        controller.Stop();
    }

    [Fact]
    public void DisabledOrIncompleteShortcutsAreIgnored()
    {
        TestKeyboardInputSource input = new();
        TestHistory history = new();
        DesktopHistoryConfiguration configuration = new();
        DesktopHistoryShortcutController controller = new(input, history, configuration);
        controller.Start();
        input.KeysDown.Add(0x5B);

        KeyEventArgs incomplete = input.RaiseDown(0xDB);
        configuration.Update(false, 100, true, null, null);
        input.KeysDown.Add(0xA2);
        KeyEventArgs disabled = input.RaiseDown(0xDB);

        Assert.False(incomplete.Handled);
        Assert.False(disabled.Handled);
        Assert.Equal(0, history.BackCount);

        controller.Stop();
    }

    private sealed class TestKeyboardInputSource : IKeyboardInputSource
    {
        public event EventHandler<KeyEventArgs>? KeyDown;
        public event EventHandler<KeyEventArgs>? KeyUp;

        public HashSet<int> KeysDown { get; } = [];

        public bool IsKeyDown(int virtualKeyCode) => KeysDown.Contains(virtualKeyCode);

        public KeyEventArgs RaiseDown(int virtualKeyCode)
        {
            KeyEventArgs args = new(virtualKeyCode);
            KeyDown?.Invoke(this, args);
            return args;
        }

        public KeyEventArgs RaiseUp(int virtualKeyCode)
        {
            KeysDown.Remove(virtualKeyCode);
            KeyEventArgs args = new(virtualKeyCode);
            KeyUp?.Invoke(this, args);
            return args;
        }

        public void Dispose() => GC.SuppressFinalize(this);
    }

    private sealed class TestHistory : IDesktopNavigationHistory
    {
        public event EventHandler? Changed;

        public bool IsEnabled => true;
        public bool CanGoBack => true;
        public bool CanGoForward => true;
        public IReadOnlyList<DesktopHistoryEntry> BackEntries => [];
        public IReadOnlyList<DesktopHistoryEntry> ForwardEntries => [];
        public int BackCount { get; private set; }
        public int ForwardCount { get; private set; }

        public bool GoBack()
        {
            BackCount++;
            return true;
        }

        public bool GoForward()
        {
            ForwardCount++;
            return true;
        }

        public bool NavigateTo(long entryId) => false;

        public void Clear() => Changed?.Invoke(this, EventArgs.Empty);
    }
}

public class DesktopHistoryShortcutValidatorTests
{
    [Fact]
    public void EquivalentShortcutsMatchModifierGroupsInEitherOrder()
    {
        List<List<int>> left = [[0x5B, 0x5C], [0xA2, 0xA3], [0xDB]];
        List<List<int>> right = [[0xA2], [0x5B], [0xDB]];

        Assert.True(DesktopHistoryShortcutValidator.AreEquivalent(left, right));
        Assert.False(DesktopHistoryShortcutValidator.AreEquivalent(left, [[0xA2], [0x5B], [0xDD]]));
    }

    [Fact]
    public void PageShortcutConflictRequiresMatchingModifiersAndPageTrigger()
    {
        List<List<int>> modifiers = [[0x5B, 0x5C], [0xA2, 0xA3]];

        Assert.True(DesktopHistoryShortcutValidator.ConflictsWithPageNavigation(
            [[0x5B], [0xA2], [0x27]], modifiers));
        Assert.False(DesktopHistoryShortcutValidator.ConflictsWithPageNavigation(
            [[0x5B], [0xA2], [0xDB]], modifiers));
    }
}
