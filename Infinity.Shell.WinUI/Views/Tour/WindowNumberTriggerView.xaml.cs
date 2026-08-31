using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class WindowNumberTriggerView : UserControl
{
    public WindowNumberTriggerView() => InitializeComponent();

    public WindowNumberTriggerViewModel ViewModel => (WindowNumberTriggerViewModel)DataContext;
}
