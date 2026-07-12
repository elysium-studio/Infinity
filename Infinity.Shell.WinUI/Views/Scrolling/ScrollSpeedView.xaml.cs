using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class ScrollSpeedView :
    UserControl
{
    public ScrollSpeedView() => InitializeComponent();

    public ScrollSpeedViewModel ViewModel => (ScrollSpeedViewModel)DataContext;
}
