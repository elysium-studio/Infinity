using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class VirtualPagesView :
    UserControl
{
    public VirtualPagesView() => InitializeComponent();

    public VirtualPagesViewModel? ViewModel => DataContext as VirtualPagesViewModel;
}
