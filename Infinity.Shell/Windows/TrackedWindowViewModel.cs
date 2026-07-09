using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public partial class TrackedWindowViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IWindowController controller,
    IWindowPreviewSurface windowPreviewSurface,
    IntPtr handle) :
    ObservableViewModel(provider, factory, messenger, disposer),
    ITrackedWindow
{
    private readonly IWindowPreview? preview = windowPreviewSurface.CreatePreview(handle);
    private IntPtr previewTargetHandle;
    private double previewWidth;
    private double previewHeight;

    [ObservableProperty]
    private double height;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldFadeThumb))]
    private bool isFiltered;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private object? thumbnail;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private double width;

    [ObservableProperty]
    private double x;

    [ObservableProperty]
    private double y;

    [ObservableProperty]
    private int? zIndex;

    public IntPtr Handle { get; } = handle;

    public bool ShouldFadeThumb => IsFiltered;

    public IWindowPreview? Preview => preview;

    public IWindowPreview? Preview1 => preview;

    public void BeginPeek() => Messenger.Send(new WindowPeekChangedEventArgs(Handle, true));

    public void Close() => controller.Close(Handle);

    public void EndPeek() => Messenger.Send(new WindowPeekChangedEventArgs(Handle, false));

    public void Navigate() => Messenger.Send(new WindowNavigationRequestedEventArgs(Handle));

    public void SetPreviewTarget(IntPtr sharedTargetHandle, double width, double height)
    {
        if (previewTargetHandle == sharedTargetHandle &&
            Math.Abs(previewWidth - width) < 0.5 &&
            Math.Abs(previewHeight - height) < 0.5)
        {
            return;
        }

        previewTargetHandle = sharedTargetHandle;
        previewWidth = width;
        previewHeight = height;

        UpdatePreview();
    }

    public void SetPreviewPlacement(double x, double y, double width, double height)
    {
        if (Math.Abs(previewWidth - width) < 0.5 &&
            Math.Abs(previewHeight - height) < 0.5)
        {
            return;
        }

        previewWidth = width;
        previewHeight = height;

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (previewTargetHandle == 0 || previewWidth <= 0.0 || previewHeight <= 0.0)
        {
            preview?.SetTarget(0, 0.0, 0.0, false);
            return;
        }

        preview?.SetTarget(previewTargetHandle, previewWidth, previewHeight, true);
    }
}