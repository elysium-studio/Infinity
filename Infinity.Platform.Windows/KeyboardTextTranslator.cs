using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Infinity.Platform.Windows;

public sealed class KeyboardTextTranslator : IKeyboardTextTranslator
{
    private const int KeyboardStateLength = 256;
    private const int TranslationBufferLength = 8;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyMenu = 0x12;
    private const int VirtualKeyLeftWindows = 0x5B;
    private const int VirtualKeyRightWindows = 0x5C;
    private const int VirtualKeyLeftControl = 0xA2;
    private const int VirtualKeyRightControl = 0xA3;
    private const int VirtualKeyLeftMenu = 0xA4;
    private const int VirtualKeyRightMenu = 0xA5;

    public unsafe string? Translate(int virtualKeyCode)
    {
        if (!IsTextInputKey(virtualKeyCode))
        {
            return null;
        }

        Span<byte> keyboardState = stackalloc byte[KeyboardStateLength];
        if (!PInvoke.GetKeyboardState(keyboardState))
        {
            return null;
        }

        bool controlDown = IsAnyKeyDown(keyboardState, VirtualKeyControl, VirtualKeyLeftControl, VirtualKeyRightControl);
        bool menuDown = IsAnyKeyDown(keyboardState, VirtualKeyMenu, VirtualKeyLeftMenu, VirtualKeyRightMenu);
        bool windowsDown = IsAnyKeyDown(keyboardState, VirtualKeyLeftWindows, VirtualKeyRightWindows);
        if (!IsTextEntryChord(controlDown, menuDown, windowsDown))
        {
            return null;
        }

        uint scanCode = PInvoke.MapVirtualKey((uint)virtualKeyCode, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);
        Span<char> buffer = stackalloc char[TranslationBufferLength];
        int length = PInvoke.ToUnicodeEx((uint)virtualKeyCode, scanCode, keyboardState, buffer, 0, null);
        return length > 0 ? new string (buffer[..length]) : null;
    }


    internal static bool IsTextInputKey(int virtualKeyCode) => virtualKeyCode == 0x20 || virtualKeyCode is >= 0x30 and <= 0x39 || virtualKeyCode is >= 0x41 and <= 0x5A || virtualKeyCode is >= 0x60 and <= 0x6F || virtualKeyCode is >= 0xBA and <= 0xC0 || virtualKeyCode is >= 0xDB and <= 0xDF || virtualKeyCode == 0xE2;

    internal static bool IsTextEntryChord(bool controlDown, bool menuDown, bool windowsDown) => !windowsDown && controlDown == menuDown;

    private static bool IsAnyKeyDown(ReadOnlySpan<byte> keyboardState, int firstVirtualKey, int secondVirtualKey) => IsKeyDown(keyboardState, firstVirtualKey) || IsKeyDown(keyboardState, secondVirtualKey);

    private static bool IsAnyKeyDown(ReadOnlySpan<byte> keyboardState, int firstVirtualKey, int secondVirtualKey, int thirdVirtualKey) => IsKeyDown(keyboardState, firstVirtualKey) || IsKeyDown(keyboardState, secondVirtualKey) || IsKeyDown(keyboardState, thirdVirtualKey);

    private static bool IsKeyDown(ReadOnlySpan<byte> keyboardState, int virtualKey) => (keyboardState[virtualKey] & 0x80) != 0;
}
