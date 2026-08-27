using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class WindowJumpTriggerView : UserControl
{
    public WindowJumpTriggerView() => InitializeComponent();

    public WindowJumpTriggerViewModel ViewModel => (WindowJumpTriggerViewModel)DataContext;
}
