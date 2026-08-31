using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopView :
    UserControl
{
    public DesktopView() => InitializeComponent();

    public DesktopViewModel ViewModel => (DesktopViewModel)DataContext;
}