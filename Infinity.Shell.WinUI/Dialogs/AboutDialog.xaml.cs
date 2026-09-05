using Infinity.Application.Abstractions;
using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog(AboutViewModel viewModel, ITextLocalizer localizer)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = localizer.GetText("AboutDialogTitle");
        CloseButtonText = localizer.GetText("AboutDialogCloseButton");
    }


    public AboutViewModel ViewModel { get; }
}
