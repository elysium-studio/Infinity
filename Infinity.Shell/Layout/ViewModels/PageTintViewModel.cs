using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Options;
using NavigationCompletedEventArgs = Infinity.Application.Abstractions.NavigationCompletedEventArgs;

namespace Infinity.Shell;

public sealed partial class PageTintViewModel :
    ObservableViewModel,
    IRecipient<NavigationCompletedEventArgs>,
    IRecipient<WindowActivationRequestedEventArgs>,
    IRecipient<FilterChangedEventArgs>,
    IRecipient<OptionsChangedEventArgs<Settings>>
{
    private readonly IDispatcher dispatcher;
    private readonly IModifierKeyState modifierKeyState;
    private readonly IOptionsMonitor<Settings> settings;
    private readonly IWritableOptions<Settings> writableOptions;
    private readonly IPager pager;
    private readonly ITextLocalizer localizer;
    private readonly IInfinityGlanceBridge glanceBridge;
    private bool filterActive;

    [ObservableProperty]
    private bool isBlurEnabled;

    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private bool staysOpen;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string pageTitle = string.Empty;

    [ObservableProperty]
    private string editingTitle = string.Empty;

    [ObservableProperty]
    private PreviewPosition previewPosition;

    [ObservableProperty]
    private bool isGlancePageSurfaceAvailable;

    partial void OnIsOpenChanged(bool value) =>
        glanceBridge.SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface.PageTint, value);

    public PageTintViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, IPointerInputSource pointer, IModifierKeyState modifierKeyState, IWindowDragScroller dragScroller, IPageGestureSource gestureSource, IOptionsMonitor<Settings> settings, IWritableOptions<Settings> writableOptions, IPager pager, IPanState panState, IInfinityGlanceBridge glanceBridge, ITextLocalizer localizer) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.modifierKeyState = modifierKeyState;
        this.settings = settings;
        this.writableOptions = writableOptions;
        this.pager = pager;
        this.glanceBridge = glanceBridge;
        this.localizer = localizer;

        isBlurEnabled = settings.CurrentValue.DesktopBlur;
        previewPosition = settings.CurrentValue.PreviewPosition;
        isGlancePageSurfaceAvailable = glanceBridge.IsPageNavigationAvailable;

        pointer.ScrollDeltaReceived += HandleScrollDeltaReceived;
        pointer.MiddleButtonClicked += HandleMiddleButtonClicked;
        pager.PageChanged += HandlePageChanged;
        dragScroller.DragStarted += HandleDragStarted;
        dragScroller.DragStopped += HandleDragStopped;
        gestureSource.SessionStarted += HandleGestureSessionStarted;
        gestureSource.SessionEnded += HandleGestureSessionEnded;
        glanceBridge.AvailabilityChanged += HandleGlanceAvailabilityChanged;

        Activate();
    }

    public override void Activated()
    {
        PageTitle = ResolvePageTitle(pager.CurrentPage, settings.CurrentValue.PageTitles);
        PublishPageNavigation();
    }

    protected override void RegisterMessages()
    {
        Messenger.Register<NavigationCompletedEventArgs>(this);
        Messenger.Register<WindowActivationRequestedEventArgs>(this);
        Messenger.Register<FilterChangedEventArgs>(this);
        Messenger.Register<OptionsChangedEventArgs<Settings>>(this);
    }

    public void BeginEditing()
    {
        EditingTitle = PageTitle;
        IsEditing = true;
    }

    public async Task CommitEditAsync()
    {
        string trimmed = EditingTitle.Trim();
        int page = pager.CurrentPage;

        Settings updated = await writableOptions.ReadAsync() ?? new Settings();
        updated.PageTitles ??= [];

        if (string.IsNullOrEmpty(trimmed))
        {
            updated.PageTitles.Remove(page);
        }
        else
        {
            updated.PageTitles[page] = trimmed;
        }

        await writableOptions.WriteAsync(updated);

        dispatcher.Dispatch(() =>
        {
            PageTitle = ResolvePageTitle(page, settings.CurrentValue.PageTitles);
            IsEditing = false;
            PublishPageNavigation();
        });
    }

    public void CancelEditing() => IsEditing = false;

    public void Receive(NavigationCompletedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            if (!filterActive)
            {
                IsOpen = false;
            }
        });

    public void Receive(WindowActivationRequestedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            if (!filterActive)
            {
                IsOpen = false;
            }
        });

    public void Receive(FilterChangedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            filterActive = args.IsActive;
            IsOpen = args.IsActive;
        });

    public void Receive(OptionsChangedEventArgs<Settings> args) =>
        dispatcher.Dispatch(() =>
        {
            IsBlurEnabled = args.Options.DesktopBlur;
            PreviewPosition = args.Options.PreviewPosition;
        });

    private void HandleDragStarted() =>
        dispatcher.Dispatch(() =>
        {
            StaysOpen = true;
            PageTitle = ResolvePageTitle(pager.CurrentPage, settings.CurrentValue.PageTitles);
            IsOpen = true;
        });

    private void HandleDragStopped() =>
        dispatcher.Dispatch(() =>
        {
            StaysOpen = false;
            IsOpen = false;
        });

    private void HandleGestureSessionStarted() =>
        dispatcher.Dispatch(() =>
        {
            StaysOpen = true;
            PageTitle = ResolvePageTitle(pager.CurrentPage, settings.CurrentValue.PageTitles);
            IsOpen = true;
        });

    private void HandleGestureSessionEnded() =>
        dispatcher.Dispatch(() =>
        {
            StaysOpen = false;
            IsOpen = false;
        });

    private void HandlePageChanged(int page) =>
        dispatcher.Dispatch(() =>
        {
            IsEditing = false;
            PageTitle = ResolvePageTitle(page, settings.CurrentValue.PageTitles);
            PublishPageNavigation();
        });

    private void HandleScrollDeltaReceived(int delta)
    {
        if (modifierKeyState.IsActive)
        {
            dispatcher.Dispatch(() =>
            {
                IsOpen = true;
            });
        }
    }

    private void HandleMiddleButtonClicked()
    {
        if (modifierKeyState.IsActive)
        {
            dispatcher.Dispatch(() =>
            {
                IsOpen = true;
            });
        }
    }

    private void HandleGlanceAvailabilityChanged(object? sender, InfinityGlanceAvailabilityChangedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            IsGlancePageSurfaceAvailable = args.IsPageNavigationAvailable;
        });

    private void PublishPageNavigation() =>
        glanceBridge.PublishPageNavigation(new InfinityPageNavigationState(pager.CurrentPage, pager.CurrentPage + 1, PageTitle));

    private string ResolvePageTitle(int page, Dictionary<int, string>? pageTitles) =>
        pageTitles?.TryGetValue(page, out string? title) == true
            ? title
            : localizer.GetText("PageTitle", page + 1);
}
