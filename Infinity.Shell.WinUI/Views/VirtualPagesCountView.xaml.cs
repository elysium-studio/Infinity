using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class VirtualPagesCountView :
    UserControl
{
    public VirtualPagesCountView() => InitializeComponent();

    public VirtualPagesCountViewModel ViewModel => (VirtualPagesCountViewModel)DataContext;
}