using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public partial class TrackedWindowViewModel :
    ObservableViewModel,
    ITrackedWindow
{
    private readonly IWindowPreview? preview;
    private readonly IWindowController controller;
    private readonly Action<IntPtr> navigate;
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

    public TrackedWindowViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IWindowController controller,
        IWindowPreviewSurface windowPreviewSurface,
        IntPtr handle,
        Action<IntPtr> navigate) : base(provider, factory, messenger, disposer)
    {
        this.controller = controller;
        this.navigate = navigate;
        preview = windowPreviewSurface.CreatePreview(handle);
        Handle = handle;
    }

    public IntPtr Handle { get; }

    public bool ShouldFadeThumb => IsFiltered;

    public IWindowPreview? Preview => preview;

    public IWindowPreview? Preview1 => preview;

    public void Close() => controller.Close(Handle);

    public void Navigate() => navigate(Handle);

    public void SetPreviewTarget(IntPtr sharedTargetHandle, double width, double height)
    {
        previewTargetHandle = sharedTargetHandle;
        previewWidth = width;
        previewHeight = height;

        UpdatePreview();
    }

    public void SetPreviewPlacement(double x, double y, double width, double height)
    {
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