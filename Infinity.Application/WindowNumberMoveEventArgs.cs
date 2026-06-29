namespace Infinity.Application;

public class WindowNumberMoveEventArgs(int virtualKeyCode)
{
    public int VirtualKeyCode { get; } = virtualKeyCode;
}
