namespace Infinity.Platform.Abstractions;

public readonly struct WindowMoveScope(Action end) : IDisposable
{
    public void Dispose() => end();
}
