using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class ScrollTriggerView : UserControl
{
    public ScrollTriggerView() => InitializeComponent();

    public ScrollTriggerViewModel ViewModel => (ScrollTriggerViewModel)DataContext;
}
