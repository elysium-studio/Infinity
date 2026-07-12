using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infinity.Shell;

public partial class TrackedWindowViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IWindowController controller,
    IWindowPreviewSurface windowPreviewSurface,
    IWindowPageMover pageMover,
    IWindowPlacementRules placementRules,
    IPager pager,
    IOptionsMonitor<Settings> settings,
    ITextLocalizer localizer,
    ILogger<TrackedWindowViewModel> logger,
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

    public bool CanCreatePlacementRule => placementRules.CanCreateRule(Handle);

    public void BeginPeek() => Messenger.Send(new WindowPeekChangedEventArgs(Handle, true));

    public void Close() => controller.Close(Handle);

    public void EndPeek() => Messenger.Send(new WindowPeekChangedEventArgs(Handle, false));

    public void Navigate() => Messenger.Send(new WindowNavigationRequestedEventArgs(Handle));

    public IReadOnlyList<WindowPageTarget> GetPageTargets()
    {
        int existingPageCount = pager.PageCount;
        int targetCount = pager.MaxPages ?? existingPageCount + 1;
        Dictionary<int, string>? pageTitles = settings.CurrentValue.PageTitles;
        List<WindowPageTarget> targets = new(targetCount);

        for (int page = 0; page < targetCount; page++)
        {
            string displayName;

            if (pager.MaxPages is null && page == existingPageCount)
            {
                displayName = localizer.GetText("NewPageTitle", page + 1);
            }
            else if (pageTitles?.TryGetValue(page, out string? title) == true && !string.IsNullOrWhiteSpace(title))
            {
                displayName = localizer.GetText("PageWithTitle", page + 1, title);
            }
            else
            {
                displayName = localizer.GetText("PageTitle", page + 1);
            }

            targets.Add(new WindowPageTarget(page, displayName));
        }

        return targets;
    }

    public int? GetCurrentPage() =>
        pageMover.TryGetPage(Handle, out int page) ? page : null;

    public int? GetOpeningPage() =>
        placementRules.TryGetTargetPage(Handle, out int page) ? page : null;

    public void MoveToPage(int page) => pageMover.MoveToPage(Handle, page);

    public async Task RemoveOpeningPageRuleAsync()
    {
        try
        {
            await placementRules.RemoveAsync(Handle);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to remove the application page rule for window {Handle}", Handle);
        }
    }

    public async Task SetOpeningPageAsync(int page)
    {
        try
        {
            await placementRules.SetTargetPageAsync(Handle, page);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to save the application page rule for window {Handle}", Handle);
        }
    }

    public void SetPreviewTarget(IntPtr sharedTargetHandle, double width, double height)
    {
        if (previewTargetHandle == sharedTargetHandle
            && Math.Abs(previewWidth - width) < 0.5
            && Math.Abs(previewHeight - height) < 0.5)
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
