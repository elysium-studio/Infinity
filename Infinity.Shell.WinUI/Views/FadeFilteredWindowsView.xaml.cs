using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class FadeFilteredWindowsView :
    UserControl
{
    public FadeFilteredWindowsView() => InitializeComponent();

    public FadeFilteredWindowsViewModel ViewModel => (FadeFilteredWindowsViewModel)DataContext;
}