using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class PreviewPositionView :
    UserControl
{
    public PreviewPositionView() => InitializeComponent();

    public PreviewPositionViewModel ViewModel => (PreviewPositionViewModel)DataContext;
}