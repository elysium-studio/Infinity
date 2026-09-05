using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class OverviewView : UserControl
{
    public OverviewView() => InitializeComponent();

    public OverviewViewModel ViewModel => (OverviewViewModel)DataContext;
}
