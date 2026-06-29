using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public partial class ScrollModifierKeyView :
    UserControl
{
    public ScrollModifierKeyView()
    {
        InitializeComponent();
    }

    public ScrollModifierKeyViewModel? ViewModel => DataContext as ScrollModifierKeyViewModel;

    public string ToHintText(bool isRecording, int count) =>
        isRecording && count == 0 ? "Press shortcut keys..." : string.Empty;

    public Visibility ToHintVisibility(int count, bool isRecording) =>
        isRecording && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ToLabelsVisibility(int count) =>
        count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ToEditVisibility(bool isRecording) =>
        isRecording ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ToRecordingVisibility(bool isRecording) =>
        isRecording ? Visibility.Visible : Visibility.Collapsed;
}