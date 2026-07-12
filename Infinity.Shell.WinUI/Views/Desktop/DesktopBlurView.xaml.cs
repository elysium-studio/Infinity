using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class DesktopBlurView :
    UserControl
{
    public DesktopBlurView() => InitializeComponent();

    public DesktopBlurViewModel ViewModel => (DesktopBlurViewModel)DataContext;
}
