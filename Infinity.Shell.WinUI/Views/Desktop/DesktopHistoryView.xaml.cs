using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class DesktopHistoryView :
    UserControl
{
    public DesktopHistoryView() => InitializeComponent();

    public DesktopHistoryViewModel ViewModel => (DesktopHistoryViewModel)DataContext;

    public Visibility ToEntriesVisibility(int count) =>
        count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ToEmptyVisibility(int count) =>
        count == 0 ? Visibility.Visible : Visibility.Collapsed;
}
