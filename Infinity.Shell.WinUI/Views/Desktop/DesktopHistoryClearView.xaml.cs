using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class DesktopHistoryClearView :
    UserControl
{
    public DesktopHistoryClearView() => InitializeComponent();

    public DesktopHistoryClearViewModel ViewModel => (DesktopHistoryClearViewModel)DataContext;
}
