namespace Infinity.Application;

public sealed class WindowArrowMoveEventArgs(int virtualKeyCode)
{
    public int VirtualKeyCode { get; } = virtualKeyCode;
}