using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class StartWithWindowsView :
    UserControl
{
    public StartWithWindowsView() => InitializeComponent();

    public StartWithWindowsViewModel ViewModel => (StartWithWindowsViewModel)DataContext;
}