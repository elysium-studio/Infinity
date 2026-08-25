using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class WindowsView :
    UserControl
{
    public WindowsView() => InitializeComponent();

    public WindowsViewModel ViewModel => (WindowsViewModel)DataContext;
}