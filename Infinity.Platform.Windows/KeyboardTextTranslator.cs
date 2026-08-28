using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Infinity.Platform.Windows;

public sealed class KeyboardTextTranslator :
    IKeyboardTextTranslator
{
    private const int KeyboardStateLength = 256;
    private const int TranslationBufferLength = 8;

    public unsafe string? Translate(int virtualKeyCode)
    {
        if (virtualKeyCode is < 0 or >= KeyboardStateLength)
        {
            return null;
        }

        Span<byte> keyboardState = stackalloc byte[KeyboardStateLength];

        if (!PInvoke.GetKeyboardState(keyboardState))
        {
            return null;
        }

        uint scanCode = PInvoke.MapVirtualKey((uint)virtualKeyCode, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);
        Span<char> buffer = stackalloc char[TranslationBufferLength];
        int length = PInvoke.ToUnicodeEx((uint)virtualKeyCode,
            scanCode,
            keyboardState,
            buffer,
            0,
            null);

        return length > 0 ? new string(buffer[..length]) : null;
    }
}
