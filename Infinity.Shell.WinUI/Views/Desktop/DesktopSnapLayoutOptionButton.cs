using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopSnapLayoutOptionButton : ToggleButton
{
    private bool isPressed;

    public DesktopSnapLayoutOptionButton()
    {
        AddHandler(PointerPressedEvent, new PointerEventHandler(HandlePointerPressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(HandlePointerReleased), true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler(HandlePointerReleased), true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler(HandlePointerReleased), true);

        PointerEntered += HandlePointerInteractionChanged;
        PointerExited += HandlePointerExited;
        Checked += HandleSelectionChanged;
        Unchecked += HandleSelectionChanged;
        KeyDown += HandleKeyDown;
        KeyUp += HandleKeyUp;
    }

    public event RoutedEventHandler? InteractionStateChanged;

    public DesktopSnapLayoutOptionInteractionState InteractionState =>
        IsChecked == true
            ? isPressed
                ? DesktopSnapLayoutOptionInteractionState.SelectedPressed
                : IsPointerOver
                    ? DesktopSnapLayoutOptionInteractionState.SelectedPointerOver
                    : DesktopSnapLayoutOptionInteractionState.Selected
            : isPressed
                ? DesktopSnapLayoutOptionInteractionState.Pressed
                : IsPointerOver
                    ? DesktopSnapLayoutOptionInteractionState.PointerOver
                    : DesktopSnapLayoutOptionInteractionState.Normal;

    private void NotifyInteractionStateChanged() => InteractionStateChanged?.Invoke(this, new RoutedEventArgs());

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        isPressed = args.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
        NotifyInteractionStateChanged();
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        isPressed = false;
        NotifyInteractionStateChanged();
    }

    private void HandlePointerInteractionChanged(object sender, PointerRoutedEventArgs args) =>
        NotifyInteractionStateChanged();

    private void HandlePointerExited(object sender, PointerRoutedEventArgs args)
    {
        isPressed = false;
        NotifyInteractionStateChanged();
    }

    private void HandleSelectionChanged(object sender, RoutedEventArgs args) => NotifyInteractionStateChanged();

    private void HandleKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Space or VirtualKey.Enter)
        {
            isPressed = true;
            NotifyInteractionStateChanged();
        }
    }

    private void HandleKeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Space or VirtualKey.Enter)
        {
            isPressed = false;
            NotifyInteractionStateChanged();
        }
    }
}
