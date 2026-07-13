namespace Infinity.Platform.Abstractions;

public interface IPointerInputSource : 
    IDisposable
{
    event Action<int, int>? CursorMoved;

    event Action? LeftButtonClicked;

    event Action? MiddleButtonClicked;

    event Action? RightButtonClicked;

    event Action<int>? ScrollDeltaReceived;

    event Action<double>? ScrollVelocityIdle;
}