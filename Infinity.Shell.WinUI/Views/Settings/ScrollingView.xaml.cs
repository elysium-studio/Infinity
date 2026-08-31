using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class ScrollingView :
    UserControl
{
    public ScrollingView() => InitializeComponent();

    public ScrollingViewModel ViewModel => (ScrollingViewModel)DataContext;
}

