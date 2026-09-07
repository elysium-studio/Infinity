namespace Infinity.Shell;

public readonly record struct DesktopContentDragTarget(
    int Page,
    nint Window)
{
    public static DesktopContentDragTarget None => new(-1, 0);
}

public sealed class DesktopContentDragNavigation
{
    private bool modifiersHeld;

    public DesktopContentDragTarget Update(bool held, DesktopContentDragTarget target)
    {
        bool released = modifiersHeld && !held;
        modifiersHeld = held;
        return released && target.Page >= 0 ? target : DesktopContentDragTarget.None;
    }

    public void Reset() => modifiersHeld = false;
}
