using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows.DependencyInjection;

public class KeyboardInputKeysFactory(Func<List<List<int>>> factory) :
    IKeyboardInputKeysFactory
{
    public List<List<int>> Create() => factory();
}