namespace Infinity.Platform.Abstractions;

public interface IWindowMoveGuard
{
    bool IsSystemMove { get; }

    WindowMoveScope Begin();
}
