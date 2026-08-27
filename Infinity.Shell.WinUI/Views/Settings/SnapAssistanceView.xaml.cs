using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class SnapAssistanceView :
    UserControl
{
    public SnapAssistanceView() => InitializeComponent();

    public SnapAssistanceViewModel ViewModel => (SnapAssistanceViewModel)DataContext;
}
