namespace Infinity.Application;

public class WindowArrowSwitchEventArgs(int virtualKeyCode)
{
    public int VirtualKeyCode { get; } = virtualKeyCode;
}
