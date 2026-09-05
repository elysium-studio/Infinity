using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopShortcutChord : UserControl
{
    public static readonly DependencyProperty FirstModifierProperty = DependencyProperty.Register(nameof(FirstModifier), typeof(string), typeof(DesktopShortcutChord), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SecondModifierProperty = DependencyProperty.Register(nameof(SecondModifier), typeof(string), typeof(DesktopShortcutChord), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ActionLabelProperty = DependencyProperty.Register(nameof(ActionLabel), typeof(string), typeof(DesktopShortcutChord), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ShowShiftProperty = DependencyProperty.Register(nameof(ShowShift), typeof(bool), typeof(DesktopShortcutChord), new PropertyMetadata(false));

    public DesktopShortcutChord() => InitializeComponent();

    public string FirstModifier { get => (string)GetValue(FirstModifierProperty); set => SetValue(FirstModifierProperty, value); }

    public string SecondModifier { get => (string)GetValue(SecondModifierProperty); set => SetValue(SecondModifierProperty, value); }

    public string ActionLabel { get => (string)GetValue(ActionLabelProperty); set => SetValue(ActionLabelProperty, value); }

    public bool ShowShift { get => (bool)GetValue(ShowShiftProperty); set => SetValue(ShowShiftProperty, value); }


    public Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
