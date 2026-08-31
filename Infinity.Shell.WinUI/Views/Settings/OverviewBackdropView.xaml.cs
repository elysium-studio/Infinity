using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class OverviewBackdropView :
    UserControl
{
    public OverviewBackdropView() => InitializeComponent();

    public OverviewBackdropViewModel? ViewModel => DataContext as OverviewBackdropViewModel;
}
