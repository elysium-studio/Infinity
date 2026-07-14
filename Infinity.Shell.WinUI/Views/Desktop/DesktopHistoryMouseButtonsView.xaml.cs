using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class DesktopHistoryMouseButtonsView :
    UserControl
{
    public DesktopHistoryMouseButtonsView() => InitializeComponent();

    public DesktopHistoryMouseButtonsViewModel ViewModel => (DesktopHistoryMouseButtonsViewModel)DataContext;
}
