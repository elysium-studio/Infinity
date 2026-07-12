using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class PreviewBackgroundView :
    UserControl
{
    public PreviewBackgroundView() => InitializeComponent();

    public PreviewBackgroundViewModel ViewModel => (PreviewBackgroundViewModel)DataContext;
}
