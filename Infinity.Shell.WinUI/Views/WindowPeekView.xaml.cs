using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class WindowPeekView :
    UserControl
{
    public WindowPeekView() => InitializeComponent();

    public WindowPeekViewModel ViewModel => (WindowPeekViewModel)DataContext;
}