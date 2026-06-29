using Elysium.UI.Controls.WinUI;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;

namespace Infinity.Shell.WinUI;

public partial class DesktopFlyoutView :
    DesktopFlyout
{
    private readonly IWindowPreviewSurface windowPreviewSurface;

    public DesktopFlyoutView(IWindowPreviewSurface windowPreviewSurface)
    {
        InitializeComponent();

        this.windowPreviewSurface = windowPreviewSurface;

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public DesktopFlyoutViewModel ViewModel => (DesktopFlyoutViewModel)DataContext;

    public DesktopFlyoutPlacement ToPlacement(int index)
    {
        PreviewPosition position = (PreviewPosition)index;

        return position switch
        {
            PreviewPosition.Top => DesktopFlyoutPlacement.Top,
            PreviewPosition.Bottom => DesktopFlyoutPlacement.Bottom,
            PreviewPosition.Auto => DesktopFlyoutPlacement.Auto,
            _ => DesktopFlyoutPlacement.Auto
        };
    }

    private void HandleLoaded(object sender, RoutedEventArgs args) =>
        windowPreviewSurface.Initialize(Handle);

    private void HandleUnloaded(object sender, RoutedEventArgs args) =>
        windowPreviewSurface.Clear();
}