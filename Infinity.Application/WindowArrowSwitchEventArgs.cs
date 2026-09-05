namespace Infinity.Application;

public sealed class WindowArrowSwitchEventArgs(int virtualKeyCode)
{
    public int VirtualKeyCode { get; } = virtualKeyCode;
}
