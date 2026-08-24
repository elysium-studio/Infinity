using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
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
    private readonly IScroller scroller;
    private readonly IScrollPresentationSession scrollPresentationSession;
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly ITextLocalizer localizer;
    private readonly IInfinityGlanceBridge glanceBridge;
    private readonly ILogger<PageTintViewModel> logger;
    private bool filterActive;
    private bool scrollMotionActive;

    [ObservableProperty]
    private bool isBlurEnabled;

    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private bool staysOpen;

    [ObservableProperty]
    private string pageTitle = string.Empty;

    [ObservableProperty]
    private bool isDesktopPreviewActive;

    [ObservableProperty]
    private bool isDesktopPreviewCompletionRequested;

    partial void OnIsOpenChanged(bool value)
    {
        glanceBridge.SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface.PageTint, value);

        if (!value && IsDesktopPreviewActive)
        {
            scroller.CommitPresentation();
            scrollPresentationSession.End();
            IsDesktopPreviewActive = false;
            IsDesktopPreviewCompletionRequested = false;
        }
    }

    public PageTintViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, IPointerInputSource pointer, IModifierKeyState modifierKeyState, IWindowDragScroller dragScroller, IPageGestureSource gestureSource, IOptionsMonitor<Settings> settings, IWritableOptions<Settings> writableOptions, IPager pager, IPanState panState, IScroller scroller, IScrollPresentationSession scrollPresentationSession, IWindowPreviewSurface windowPreviewSurface, IInfinityGlanceBridge glanceBridge, ITextLocalizer localizer, ILogger<PageTintViewModel> logger) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.modifierKeyState = modifierKeyState;
        this.settings = settings;
        this.writableOptions = writableOptions;
        this.pager = pager;
        this.scroller = scroller;
        this.scrollPresentationSession = scrollPresentationSession;
        this.windowPreviewSurface = windowPreviewSurface;
        this.glanceBridge = glanceBridge;
        this.localizer = localizer;
        this.logger = logger;

        isBlurEnabled = settings.CurrentValue.DesktopBlur;

        pointer.ScrollDeltaReceived += HandleScrollDeltaReceived;
        pointer.MiddleButtonClicked += HandleMiddleButtonClicked;
        pager.PageChanged += HandlePageChanged;
        modifierKeyState.StateChanged += HandleModifierKeyStateChanged;
        scroller.ScrollStarted += HandleScrollerScrollStarted;
        scroller.ScrollStopped += HandleScrollerScrollStopped;
        dragScroller.DragStarted += HandleDragStarted;
        dragScroller.DragStopped += HandleDragStopped;
        gestureSource.SessionStarted += HandleGestureSessionStarted;
        gestureSource.SessionEnded += HandleGestureSessionEnded;
        glanceBridge.MessageReceived += HandleGlanceMessageReceived;

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

    public void CompleteDesktopPreview()
    {
        scrollPresentationSession.End();
        scrollMotionActive = false;
        IsDesktopPreviewActive = false;
        IsDesktopPreviewCompletionRequested = false;
        StaysOpen = false;

        if (!filterActive)
        {
            IsOpen = false;
        }
    }

    public void Receive(NavigationCompletedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            if (!filterActive && !IsDesktopPreviewActive)
            {
                IsOpen = false;
            }
        });

    public void Receive(WindowActivationRequestedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            if (!filterActive && !IsDesktopPreviewActive)
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
            if (IsDesktopPreviewActive)
            {
                return;
            }

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
            if (IsDesktopPreviewActive)
            {
                return;
            }

            StaysOpen = false;
            IsOpen = false;
        });

    private void HandlePageChanged(int page) =>
        dispatcher.Dispatch(() =>
        {
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
            BeginDesktopPreview(false);
        }
    }

    private void HandleScrollerScrollStarted(object? sender, EventArgs args) => BeginDesktopPreview(true);

    private void BeginDesktopPreview(bool scrollStarted)
    {
        if (!windowPreviewSurface.IsAvailable)
        {
            return;
        }

        scrollPresentationSession.Begin();

        dispatcher.Dispatch(() =>
        {
            scrollMotionActive |= scrollStarted;
            IsDesktopPreviewCompletionRequested = false;
            IsDesktopPreviewActive = true;
            StaysOpen = true;
            PageTitle = ResolvePageTitle(pager.CurrentPage, settings.CurrentValue.PageTitles);
            IsOpen = true;
        });
    }

    private void HandleScrollerScrollStopped(object? sender, EventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            scrollMotionActive = false;

            if (!IsDesktopPreviewActive)
            {
                scrollPresentationSession.End();
                return;
            }

            TryCompleteDesktopPreview();
        });

    private void HandleModifierKeyStateChanged(bool isActive)
    {
        if (!isActive)
        {
            dispatcher.Dispatch(TryCompleteDesktopPreview);
        }
    }

    private void TryCompleteDesktopPreview()
    {
        if (!IsDesktopPreviewActive || IsDesktopPreviewCompletionRequested ||
            scrollMotionActive || modifierKeyState.IsActive)
        {
            return;
        }

        scroller.CommitPresentation();
        IsDesktopPreviewCompletionRequested = true;
    }

    private async void HandleGlanceMessageReceived(object? sender, InfinityGlanceMessageReceivedEventArgs args)
    {
        if (!string.Equals(args.Capability, InfinityGlanceTopics.PagesCapability, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(args.Topic, InfinityGlanceTopics.PageTitleUpdate, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            InfinityPageTitleUpdate? update = JsonSerializer.Deserialize(args.Payload, InfinityGlanceJsonContext.Default.InfinityPageTitleUpdate);

            if (update is null || update.PageIndex < 0 || update.PageIndex >= pager.PageCount)
            {
                return;
            }

            await UpdatePageTitleAsync(update.PageIndex, update.PageTitle);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to update the Infinity page title from Glance");
        }
    }

    private async Task UpdatePageTitleAsync(int page, string title)
    {
        string trimmed = title.Trim();

        if (trimmed.Length > 80)
        {
            trimmed = trimmed[..80];
        }

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
            if (pager.CurrentPage != page)
            {
                return;
            }

            PageTitle = string.IsNullOrEmpty(trimmed) ? localizer.GetText("PageTitle", page + 1) : trimmed;
            PublishPageNavigation();
        });
    }

    private void PublishPageNavigation() =>
        glanceBridge.PublishPageNavigation(new InfinityPageNavigationState(pager.CurrentPage, pager.CurrentPage + 1, PageTitle));

    private string ResolvePageTitle(int page, Dictionary<int, string>? pageTitles) =>
        pageTitles?.TryGetValue(page, out string? title) == true
            ? title
            : localizer.GetText("PageTitle", page + 1);
}
