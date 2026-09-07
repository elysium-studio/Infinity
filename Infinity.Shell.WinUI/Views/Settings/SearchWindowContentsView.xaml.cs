using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class SearchWindowContentsView : UserControl
{
    public SearchWindowContentsView() => InitializeComponent();

    public SearchWindowContentsViewModel ViewModel => (SearchWindowContentsViewModel)DataContext;
}
