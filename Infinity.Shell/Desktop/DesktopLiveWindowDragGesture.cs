namespace Infinity.Shell;

public sealed class DesktopLiveWindowDragGesture
{
    private DesktopSnapPlacement previous;

    public nint Window { get; private set; }

    public bool IsMoving { get; private set; }

    public void Begin(nint window, DesktopSnapPlacement bounds)
    {
        Reset();
        if (window == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        Window = window;
        previous = bounds;
    }

    public void Update(nint window, DesktopSnapPlacement bounds)
    {
        if (Window == 0 || Window != window || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        IsMoving |= bounds.Width == previous.Width && bounds.Height == previous.Height && (bounds.CanvasX != previous.CanvasX || bounds.CanvasY != previous.CanvasY);
        previous = bounds;
    }

    public bool CanOpen(bool modifiersHeld, bool buttonHeld, bool overlayOpen) => IsMoving && modifiersHeld && buttonHeld && !overlayOpen;

    public void Reset()
    {
        Window = 0;
        IsMoving = false;
    }
}
