namespace Infinity.Shell;

public readonly record struct DesktopDragBounds(double MinimumX, double MinimumY, double MaximumX, double MaximumY)
{
    public bool IsValid => double.IsFinite(MinimumX) && double.IsFinite(MinimumY) && double.IsFinite(MaximumX) && double.IsFinite(MaximumY) && MaximumX > MinimumX && MaximumY > MinimumY;
}
