namespace Infinity.Application;

public sealed class WindowNumberSwitchEventArgs(int virtualKeyCode)
{
    public int VirtualKeyCode { get; } = virtualKeyCode;
}
