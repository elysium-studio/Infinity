using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class PageSwitchTriggerView : UserControl
{
    public PageSwitchTriggerView() => InitializeComponent();

    public PageSwitchTriggerViewModel ViewModel => (PageSwitchTriggerViewModel)DataContext;
}
