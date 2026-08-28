namespace Infinity.Platform.Abstractions;

public interface IKeyboardTextTranslator
{
    string? Translate(int virtualKeyCode);
}
