namespace Infinity.Application;

public sealed class WindowNumberMoveEventArgs(int virtualKeyCode)
{
    public int VirtualKeyCode { get; } = virtualKeyCode;
}