using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class PreviewPositionView :
    UserControl
{
    public PreviewPositionView() => InitializeComponent();

    public PreviewPositionViewModel ViewModel => (PreviewPositionViewModel)DataContext;

    public int SelectedPositionIndex
    {
        get => ViewModel.Value switch
        {
            (int)PreviewPosition.Auto => 0,
            (int)PreviewPosition.Top => 1,
            (int)PreviewPosition.Bottom => 2,
            _ => 0
        };
        set => ViewModel.Value = value switch
        {
            1 => (int)PreviewPosition.Top,
            2 => (int)PreviewPosition.Bottom,
            _ => (int)PreviewPosition.Auto
        };
    }
}
