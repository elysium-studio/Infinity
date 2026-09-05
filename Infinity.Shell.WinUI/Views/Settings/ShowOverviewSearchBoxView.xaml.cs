using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class ShowOverviewSearchBoxView : UserControl
{
    public ShowOverviewSearchBoxView() => InitializeComponent();

    public ShowOverviewSearchBoxViewModel ViewModel => (ShowOverviewSearchBoxViewModel)DataContext;
}
