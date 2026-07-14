using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class DesktopHistoryCapacityView :
    UserControl
{
    public DesktopHistoryCapacityView() => InitializeComponent();

    public DesktopHistoryCapacityViewModel ViewModel => (DesktopHistoryCapacityViewModel)DataContext;
}
