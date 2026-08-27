using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class WindowDragTriggerView : UserControl
{
    public WindowDragTriggerView() => InitializeComponent();

    public WindowDragTriggerViewModel ViewModel => (WindowDragTriggerViewModel)DataContext;
}
