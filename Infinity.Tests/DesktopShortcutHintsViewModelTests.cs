using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopShortcutHintsViewModelTests
{
    [Fact]
    public void ConfiguredModifiersUpdateWhenSettingsChange()
    {
        StrongReferenceMessenger messenger = new();
        Settings settings = new()
        {
            ScrollModifierKeys = [[0x5B], [0xA2]]
        };
        DesktopShortcutHintsViewModel viewModel = new(messenger, new TestDispatcher(), settings, new TestKeyLabelProvider());

        Assert.Equal("Key 91", viewModel.FirstModifier);
        Assert.Equal("Key 162", viewModel.SecondModifier);

        messenger.Send(new OptionsChangedEventArgs<Settings>(new Settings
        {
            ScrollModifierKeys = [[0xA4], [0xA0]]
        }));

        Assert.Equal("Key 164", viewModel.FirstModifier);
        Assert.Equal("Key 160", viewModel.SecondModifier);
    }

    private sealed class TestDispatcher :
        IDispatcher
    {
        public void Dispatch(Action action) => action();
    }

    private sealed class TestKeyLabelProvider :
        IKeyLabelProvider
    {
        public string GetFullLabel(int keyCode) => $"Key {keyCode}";

        public string GetShortLabel(int keyCode) => GetFullLabel(keyCode);

        public string Shorten(string fullText) => fullText;
    }
}
