using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed partial class ResetPageCustomizationsView : UserControl
{
    private bool isConfirmationOpen;

    public ResetPageCustomizationsView() => InitializeComponent();

    public ResetPageCustomizationsViewModel ViewModel => (ResetPageCustomizationsViewModel)DataContext;

    private async void HandleResetClicked(object sender, RoutedEventArgs args)
    {
        if (isConfirmationOpen || !ViewModel.CanReset)
        {
            return;
        }

        isConfirmationOpen = true;
        try
        {
            ContentDialog dialog = new()
            {
                Title = ViewModel.DialogTitle,
                Content = ViewModel.DialogMessage,
                PrimaryButtonText = ViewModel.DialogPrimaryButtonText,
                CloseButtonText = ViewModel.DialogCloseButtonText,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.ResetAsync();
            }
        }
        finally
        {
            isConfirmationOpen = false;
        }
    }
}
