using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class PreviewSizeView :
    UserControl
{
    public PreviewSizeView() => InitializeComponent();

    public PreviewSizeViewModel ViewModel => (PreviewSizeViewModel)DataContext;
}