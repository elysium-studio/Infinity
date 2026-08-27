using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class OverviewEdgeScrollingView :
    UserControl
{
    public OverviewEdgeScrollingView() => InitializeComponent();

    public OverviewEdgeScrollingViewModel ViewModel => (OverviewEdgeScrollingViewModel)DataContext;
}
