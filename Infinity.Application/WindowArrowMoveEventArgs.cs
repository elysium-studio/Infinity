namespace Infinity.Application;

public class WindowArrowMoveEventArgs(int virtualKeyCode)
{
    public int VirtualKeyCode { get; } = virtualKeyCode;
}
