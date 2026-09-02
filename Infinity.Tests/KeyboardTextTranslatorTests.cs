using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class KeyboardTextTranslatorTests
{
    [Theory]
    [InlineData(0x20)]
    [InlineData(0x30)]
    [InlineData(0x39)]
    [InlineData(0x41)]
    [InlineData(0x5A)]
    [InlineData(0x60)]
    [InlineData(0x6F)]
    [InlineData(0xBA)]
    [InlineData(0xDF)]
    [InlineData(0xE2)]
    public void TextInputKeysCanBeTranslated(int virtualKeyCode) =>
        Assert.True(KeyboardTextTranslator.IsTextInputKey(virtualKeyCode));

    [Theory]
    [InlineData(0x08)] // Backspace
    [InlineData(0x0D)] // Enter
    [InlineData(0x1B)] // Escape
    [InlineData(0x21)] // Page up
    [InlineData(0x25)] // Left arrow
    [InlineData(0x2C)] // Print screen
    [InlineData(0x70)] // F1
    [InlineData(0x7B)] // F12
    [InlineData(0x87)] // F24
    [InlineData(0xA6)] // Browser back
    [InlineData(0xAF)] // Volume up
    public void FunctionAndSystemKeysCannotBeTranslated(int virtualKeyCode) =>
        Assert.False(KeyboardTextTranslator.IsTextInputKey(virtualKeyCode));

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, false)]
    public void OnlyTextEntryModifierChordsCanBeTranslated(bool controlDown, bool menuDown, bool windowsDown, bool expected) =>
        Assert.Equal(expected, KeyboardTextTranslator.IsTextEntryChord(controlDown, menuDown, windowsDown));
}
