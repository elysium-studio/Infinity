using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Infinity.UI.WinUI;

public sealed class SharedBoundaryHandle : Control
{
    public static readonly DependencyProperty IsVerticalProperty = DependencyProperty.Register(nameof(IsVertical), typeof(bool), typeof(SharedBoundaryHandle), new PropertyMetadata(true, HandleOrientationChanged));

    public SharedBoundaryHandle() => DefaultStyleKey = typeof(SharedBoundaryHandle);

    public bool IsVertical
    {
        get => (bool)GetValue(IsVerticalProperty);
        set => SetValue(IsVerticalProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateOrientation();
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs args)
    {
        base.OnPointerEntered(args);
        VisualStateManager.GoToState(this, "PointerOver", false);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs args)
    {
        base.OnPointerExited(args);
        VisualStateManager.GoToState(this, "Normal", false);
    }

    protected override void OnTapped(TappedRoutedEventArgs args) => args.Handled = true;

    private static void HandleOrientationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((SharedBoundaryHandle)sender).UpdateOrientation();

    private void UpdateOrientation()
    {
        ProtectedCursor = InputSystemCursor.Create(IsVertical ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);
        VisualStateManager.GoToState(this, IsVertical ? "Vertical" : "Horizontal", false);
    }
}
