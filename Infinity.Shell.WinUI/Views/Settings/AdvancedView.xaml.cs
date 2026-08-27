using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class AdvancedView :
    UserControl
{
    public AdvancedView() => InitializeComponent();

    public AdvancedViewModel ViewModel => (AdvancedViewModel)DataContext;
}

