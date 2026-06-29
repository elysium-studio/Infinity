using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class PreviewView :
    UserControl
{
    public PreviewView() => InitializeComponent();

    public PreviewViewModel ViewModel => (PreviewViewModel)DataContext;
}