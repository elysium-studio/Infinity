namespace Infinity.Shell;

public sealed class DesktopWindowPeekGesture
{
    private uint? pointerId;
    private double startX;
    private double startY;
    private bool moved;

    public bool IsEnabled { get; set; }

    public bool IsActive(uint id) => pointerId == id;

    public bool Begin(uint id, double x, double y, bool primaryButton, bool altOnly)
    {
        if (!IsEnabled || !primaryButton || !altOnly || pointerId is not null)
        {
            return false;
        }

        pointerId = id;
        startX = x;
        startY = y;
        moved = false;
        return true;
    }

    public void Move(uint id, double x, double y)
    {
        if (IsActive(id) && (Math.Abs(x - startX) > 4 || Math.Abs(y - startY) > 4))
        {
            moved = true;
        }
    }

    public bool Complete(uint id, double x, double y)
    {
        if (!IsActive(id))
        {
            return false;
        }

        Move(id, x, y);
        bool clicked = IsEnabled && !moved;
        Cancel();
        return clicked;
    }

    public void Cancel() => pointerId = null;
}
