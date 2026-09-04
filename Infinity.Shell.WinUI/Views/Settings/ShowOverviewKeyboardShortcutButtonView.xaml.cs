using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class ShowOverviewKeyboardShortcutButtonView :
    UserControl
{
    public ShowOverviewKeyboardShortcutButtonView() => InitializeComponent();

    public ShowOverviewKeyboardShortcutButtonViewModel ViewModel => (ShowOverviewKeyboardShortcutButtonViewModel)DataContext;
}
