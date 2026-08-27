namespace Infinity.Shell;

public readonly record struct DesktopApplicationTarget(int Page, DesktopSnapLayoutKind Layout = DesktopSnapLayoutKind.None, int Slot = -1)
{
    public bool IsSnapSlot => Layout != DesktopSnapLayoutKind.None && Slot >= 0;
}
