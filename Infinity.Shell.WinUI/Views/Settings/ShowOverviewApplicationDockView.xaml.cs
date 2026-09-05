using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class ShowOverviewApplicationDockView : UserControl
{
    public ShowOverviewApplicationDockView() => InitializeComponent();

    public ShowOverviewApplicationDockViewModel ViewModel => (ShowOverviewApplicationDockViewModel)DataContext;
}
