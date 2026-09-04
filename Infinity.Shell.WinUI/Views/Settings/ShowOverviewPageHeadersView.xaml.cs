using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class ShowOverviewPageHeadersView :
    UserControl
{
    public ShowOverviewPageHeadersView() => InitializeComponent();

    public ShowOverviewPageHeadersViewModel ViewModel => (ShowOverviewPageHeadersViewModel)DataContext;
}
