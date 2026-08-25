using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class ScrollSpeedView :
    UserControl
{
    public ScrollSpeedView() => InitializeComponent();

    public ScrollSpeedViewModel ViewModel => (ScrollSpeedViewModel)DataContext;
}