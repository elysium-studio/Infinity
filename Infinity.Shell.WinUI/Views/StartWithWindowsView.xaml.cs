using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class StartWithWindowsView :
    UserControl
{
    public StartWithWindowsView() => InitializeComponent();

    public StartWithWindowsViewModel ViewModel => (StartWithWindowsViewModel)DataContext;
}
