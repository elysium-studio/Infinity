using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace Infinity.UI.WinUI;

[TemplatePart(Name = "PART_Thumb", Type = typeof(Thumb))]
public sealed partial class Splitter : Control
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(Splitter), new PropertyMetadata(Orientation.Vertical, HandleOrientationChanged));
    private Thumb? thumb;
    private bool pointerOver;

    public Splitter()
    {
        DefaultStyleKey = typeof(Splitter);
        Unloaded += HandleUnloaded;
        IsEnabledChanged += HandleIsEnabledChanged;
    }

    public event DragStartedEventHandler? DragStarted;

    public event DragDeltaEventHandler? DragDelta;

    public event DragCompletedEventHandler? DragCompleted;

    public bool IsDragging => thumb?.IsDragging == true;

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public void CancelDrag()
    {
        Thumb? current = thumb;
        try
        {
            current?.CancelDrag();
        }
        finally
        {
            current?.ReleasePointerCaptures();
        }
    }

    protected override void OnApplyTemplate()
    {
        if (thumb is not null)
        {
            CancelDrag();
            thumb.DragStarted -= HandleDragStarted;
            thumb.DragDelta -= HandleDragDelta;
            thumb.DragCompleted -= HandleDragCompleted;
            thumb.PointerCanceled -= HandlePointerCanceled;
        }

        base.OnApplyTemplate();
        thumb = GetTemplateChild("PART_Thumb") as Thumb;
        if (thumb is not null)
        {
            thumb.DragStarted += HandleDragStarted;
            thumb.DragDelta += HandleDragDelta;
            thumb.DragCompleted += HandleDragCompleted;
            thumb.PointerCanceled += HandlePointerCanceled;
        }

        UpdateOrientation();
        UpdateCommonState();
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs args)
    {
        base.OnPointerEntered(args);
        pointerOver = true;
        UpdateCommonState();
    }

    protected override void OnPointerExited(PointerRoutedEventArgs args)
    {
        base.OnPointerExited(args);
        pointerOver = false;
        UpdateCommonState();
    }

    protected override void OnTapped(TappedRoutedEventArgs args) => args.Handled = true;

    private static void HandleOrientationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        Splitter splitter = (Splitter)sender;
        splitter.CancelDrag();
        splitter.UpdateOrientation();
    }

    private void HandleDragStarted(object sender, DragStartedEventArgs args)
    {
        UpdateCommonState();
        DragStarted?.Invoke(this, args);
    }

    private void HandleDragDelta(object sender, DragDeltaEventArgs args) => DragDelta?.Invoke(this, args);

    private void HandleDragCompleted(object sender, DragCompletedEventArgs args)
    {
        UpdateCommonState();
        DragCompletedEventArgs completed = !args.Canceled && (!IsEnabled || !IsLoaded) ? new(args.HorizontalChange, args.VerticalChange, true) : args;
        DragCompleted?.Invoke(this, completed);
    }

    private void HandlePointerCanceled(object sender, PointerRoutedEventArgs args) => CancelDrag();

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        pointerOver = false;
        CancelDrag();
    }

    private void HandleIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (!IsEnabled)
        {
            CancelDrag();
        }

        UpdateCommonState();
    }

    private void UpdateCommonState() => VisualStateManager.GoToState(this, !IsEnabled ? "Disabled" : IsDragging ? "Dragging" : pointerOver ? "PointerOver" : "Normal", false);

    private void UpdateOrientation()
    {
        bool vertical = Orientation == Orientation.Vertical;
        ProtectedCursor = InputSystemCursor.Create(vertical ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);
        VisualStateManager.GoToState(this, vertical ? "Vertical" : "Horizontal", false);
    }
}
