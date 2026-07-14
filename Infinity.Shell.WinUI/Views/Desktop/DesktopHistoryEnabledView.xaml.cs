using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class DesktopHistoryEnabledView :
    UserControl
{
    public DesktopHistoryEnabledView() => InitializeComponent();

    public DesktopHistoryEnabledViewModel ViewModel => (DesktopHistoryEnabledViewModel)DataContext;
}
