namespace Infinity.Shell;

public readonly record struct DesktopApplicationTarget(int Page, DesktopSnapLayoutKind Layout = DesktopSnapLayoutKind.None, int Slot = -1)
{
    public bool IsSnapSlot => Layout != DesktopSnapLayoutKind.None && Slot >= 0;
}

public readonly record struct DesktopApplicationPlacement(double CanvasX, double CanvasY, double Width, double Height, bool Resize);
