using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class PreviewBackgroundView :
    UserControl
{
    public PreviewBackgroundView() => InitializeComponent();

    public PreviewBackgroundViewModel ViewModel => (PreviewBackgroundViewModel)DataContext;
}