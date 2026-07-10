using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class PreviewView :
    UserControl
{
    public PreviewView() => InitializeComponent();

    public PreviewViewModel ViewModel => (PreviewViewModel)DataContext;
}
