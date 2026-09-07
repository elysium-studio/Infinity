using System;
using System.Threading.Tasks;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopWindowResizePreview : UserControl
{
    private readonly nint windowHandle;
    private readonly IWindowIconSource icons;
    private readonly ILogger logger;
    private readonly DispatcherQueue dispatcher;
    private bool iconRequested;
    private bool iconLoaded;
    private bool isShown;

    public DesktopWindowResizePreview(
        nint windowHandle,
        IWindowIconSource icons,
        double scale,
        ILogger logger)
    {
        InitializeComponent();
        this.windowHandle = windowHandle;
        this.icons = icons;
        this.logger = logger;
        dispatcher = DispatcherQueue;
        IconSurface.Width = 32 / scale;
        IconSurface.Height = 32 / scale;
        Surface.BorderThickness = new(1 / scale);
    }

    public void Show(CornerRadius cornerRadius)
    {
        Surface.CornerRadius = cornerRadius;
        Visibility = Visibility.Visible;
        if (!isShown && !iconRequested && !iconLoaded)
        {
            iconRequested = true;
            _ = LoadIconAsync();
        }

        isShown = true;
    }

    public void Hide()
    {
        isShown = false;
        Visibility = Visibility.Collapsed;
    }

    private async Task LoadIconAsync()
    {
        try
        {
            ApplicationIcon? icon = await icons.GetIconAsync(windowHandle).ConfigureAwait(false);
            if (!dispatcher.TryEnqueue(() => ApplyIcon(icon)))
            {
                logger.LogWarning("The UI queue rejected the resize icon for window {WindowHandle}", windowHandle);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not load the resize icon for window {WindowHandle}", windowHandle);
            dispatcher.TryEnqueue(() => iconRequested = false);
        }
    }

    private void ApplyIcon(ApplicationIcon? icon)
    {
        try
        {
            ImageSource? source = DesktopScrollPreviewView.CreateApplicationIconSource(icon);
            if (source is not null)
            {
                ApplicationIcon.Source = source;
                ApplicationIcon.Visibility = Visibility.Visible;
                FallbackIcon.Visibility = Visibility.Collapsed;
                iconLoaded = true;
            }
            else
            {
                logger.LogWarning("No usable resize icon was returned for window {WindowHandle}", windowHandle);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not display the resize icon for window {WindowHandle}", windowHandle);
        }
        finally
        {
            iconRequested = false;
        }
    }
}
