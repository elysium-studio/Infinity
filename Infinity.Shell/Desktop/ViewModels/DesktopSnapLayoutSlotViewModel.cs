using CommunityToolkit.Mvvm.ComponentModel;

namespace Infinity.Shell;

public sealed partial class DesktopSnapLayoutSlotViewModel(double x, double y, double width, double height) : ObservableObject
{
    private DesktopSnapLayoutOptionInteractionState interactionState;

    public double X { get; } = x;

    public double Y { get; } = y;

    public double Width { get; } = width;

    public double Height { get; } = height;

    public bool IsPointerOver => interactionState == DesktopSnapLayoutOptionInteractionState.PointerOver;

    public bool IsPressed => interactionState == DesktopSnapLayoutOptionInteractionState.Pressed;

    public bool IsSelected => interactionState == DesktopSnapLayoutOptionInteractionState.Selected;

    public bool IsSelectedPointerOver => interactionState == DesktopSnapLayoutOptionInteractionState.SelectedPointerOver;

    public bool IsSelectedPressed => interactionState == DesktopSnapLayoutOptionInteractionState.SelectedPressed;

    public void SetInteractionState(DesktopSnapLayoutOptionInteractionState state)
    {
        if (interactionState == state)
        {
            return;
        }

        interactionState = state;
        OnPropertyChanged(nameof(IsPointerOver));
        OnPropertyChanged(nameof(IsPressed));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(IsSelectedPointerOver));
        OnPropertyChanged(nameof(IsSelectedPressed));
    }
}
