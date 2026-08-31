namespace Infinity.Shell.WinUI;

internal readonly record struct DesktopWindowDragCompletion(
    nint Handle,
    double HorizontalDelta,
    double VerticalDelta,
    DesktopWindowSnapTarget? SnapTarget,
    bool IsGroupDrag,
    bool WasMoved);
