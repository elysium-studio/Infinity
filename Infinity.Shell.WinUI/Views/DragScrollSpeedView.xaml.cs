using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class DragScrollSpeedView :
    UserControl
{
    public DragScrollSpeedView() => InitializeComponent();

    public DragScrollSpeedViewModel ViewModel => (DragScrollSpeedViewModel)DataContext;
}
