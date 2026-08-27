using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class PageNumberSwitchTriggerView : UserControl
{
    public PageNumberSwitchTriggerView() => InitializeComponent();

    public PageNumberSwitchTriggerViewModel ViewModel => (PageNumberSwitchTriggerViewModel)DataContext;
}
