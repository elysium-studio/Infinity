using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class VirtualPagesView :
    UserControl
{
    public VirtualPagesView() => InitializeComponent();

    public VirtualPagesViewModel? ViewModel => DataContext as VirtualPagesViewModel;
}