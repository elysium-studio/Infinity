using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class SpanCompatibleDisplaysView :
    UserControl
{
    public SpanCompatibleDisplaysView() => InitializeComponent();

    public SpanCompatibleDisplaysViewModel ViewModel => (SpanCompatibleDisplaysViewModel)DataContext;
}
