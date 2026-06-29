namespace Infinity.Application;

public class WindowNumberSwitchEventArgs(int virtualKeyCode)
{
    public int VirtualKeyCode { get; } = virtualKeyCode;
}
