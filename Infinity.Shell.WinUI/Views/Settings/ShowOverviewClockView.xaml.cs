using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class ShowOverviewClockView : UserControl
{
    public ShowOverviewClockView() => InitializeComponent();

    public ShowOverviewClockViewModel ViewModel => (ShowOverviewClockViewModel)DataContext;
}
