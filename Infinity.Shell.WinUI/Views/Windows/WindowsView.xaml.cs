using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class WindowsView :
    UserControl
{
    public WindowsView() => InitializeComponent();

    public WindowsViewModel ViewModel => (WindowsViewModel)DataContext;
}
