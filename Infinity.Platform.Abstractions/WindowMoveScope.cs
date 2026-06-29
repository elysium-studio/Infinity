namespace Infinity.Platform.Abstractions;

public readonly struct WindowMoveScope :
    IDisposable
{
    private readonly Action end;

    public WindowMoveScope(Action end)
    {
        this.end = end;
    }

    public void Dispose() => end();
}