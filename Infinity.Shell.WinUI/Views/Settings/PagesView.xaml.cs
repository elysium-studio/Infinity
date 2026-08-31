using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class PagesView :
    UserControl
{
    public PagesView() => InitializeComponent();

    public PagesViewModel ViewModel => (PagesViewModel)DataContext;
}

