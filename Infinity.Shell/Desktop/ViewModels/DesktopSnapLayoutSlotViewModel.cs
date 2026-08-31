using CommunityToolkit.Mvvm.ComponentModel;

namespace Infinity.Shell;

public sealed partial class DesktopSnapLayoutSlotViewModel(double x, double y, double width, double height) : ObservableObject
{
    [ObservableProperty]
    private bool isHighlighted;

    public double X { get; } = x;

    public double Y { get; } = y;

    public double Width { get; } = width;

    public double Height { get; } = height;
}
