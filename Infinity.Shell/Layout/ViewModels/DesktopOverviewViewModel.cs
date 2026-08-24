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

public sealed partial class DesktopOverviewViewModel :
    ObservableViewModel,
    IRecipient<NavigationCompletedEventArgs>,
    IRecipient<WindowActivationRequestedEventArgs>,
    IRecipient<FilterChangedEventArgs>,
    IRecipient<OptionsChangedEventArgs<Settings>>
{
    private readonly IDispatcher dispatcher;
    private readonly IModifierKeyState modifierKeyState;
    private readonly IOptionsMonitor<Settings> settings;
    private readonly IPager pager;
    private readonly IScroller scroller;
    private readonly IScrollPresentationSession scrollPresentationSession;
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly IWindowNavigationCoordinator windowNavigationCoordinator;
    private readonly IInfinityGlanceBridge glanceBridge;
    private bool filterActive;
    private bool dismissAfterPageNavigation;
    private nint pendingActivation;

    [ObservableProperty]
    private bool isBlurEnabled;

    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private bool staysOpen;

    [ObservableProperty]
    private bool isDesktopPreviewActive;

    [ObservableProperty]
    private bool isDesktopPreviewCompletionRequested;
    
    partial void OnIsOpenChanged(bool value)
    {
        glanceBridge.SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface.DesktopOverview, value);

        if (!value && IsDesktopPreviewActive)
        {
            scroller.CommitPresentation();
            scrollPresentationSession.End();
            IsDesktopPreviewActive = false;
            IsDesktopPreviewCompletionRequested = false;
            dismissAfterPageNavigation = false;
            pendingActivation = 0;
        }
    }

    public DesktopOverviewViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, IPointerInputSource pointer, IModifierKeyState modifierKeyState, IWindowDragScroller dragScroller, IPageGestureSource gestureSource, IOptionsMonitor<Settings> settings, IPager pager, IScroller scroller, IScrollPresentationSession scrollPresentationSession, IWindowPreviewSurface windowPreviewSurface, IWindowNavigationCoordinator windowNavigationCoordinator, IInfinityGlanceBridge glanceBridge) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.modifierKeyState = modifierKeyState;
        this.settings = settings;
        this.pager = pager;
        this.scroller = scroller;
        this.scrollPresentationSession = scrollPresentationSession;
        this.windowPreviewSurface = windowPreviewSurface;
        this.windowNavigationCoordinator = windowNavigationCoordinator;
        this.glanceBridge = glanceBridge;

        isBlurEnabled = settings.CurrentValue.DesktopBlur;

        pointer.ScrollDeltaReceived += HandleScrollDeltaReceived;
        pointer.MiddleButtonClicked += HandleMiddleButtonClicked;
        scroller.ScrollStarted += HandleScrollerScrollStarted;
        scroller.ScrollStopped += HandleScrollerScrollStopped;
        dragScroller.DragStarted += HandleDragStarted;
        dragScroller.DragStopped += HandleDragStopped;
        gestureSource.SessionStarted += HandleGestureSessionStarted;
        gestureSource.SessionEnded += HandleGestureSessionEnded;

        Activate();
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
        nint activation = pendingActivation;
        dismissAfterPageNavigation = false;
        pendingActivation = 0;
        scrollPresentationSession.End();
        IsDesktopPreviewActive = false;
        IsDesktopPreviewCompletionRequested = false;
        StaysOpen = false;
        IsOpen = false;

        if (activation != 0)
        {
            windowNavigationCoordinator.NavigateTo(activation);
        }
    }

    public void DismissDesktopPreview()
    {
        if (!IsDesktopPreviewActive || IsDesktopPreviewCompletionRequested)
        {
            return;
        }

        scroller.Reset();
        scroller.CommitPresentation();
        dismissAfterPageNavigation = false;
        IsDesktopPreviewCompletionRequested = true;
    }

    public void ActivateWindow(nint handle)
    {
        if (handle == 0 || !IsDesktopPreviewActive || IsDesktopPreviewCompletionRequested)
        {
            return;
        }

        pendingActivation = handle;
        DismissDesktopPreview();
    }

    public void SelectPage(int page)
    {
        if (!IsDesktopPreviewActive || IsDesktopPreviewCompletionRequested)
        {
            return;
        }

        pendingActivation = 0;

        if (page == pager.CurrentPage)
        {
            DismissDesktopPreview();
            return;
        }

        dismissAfterPageNavigation = true;
        pager.NavigateToPage(page);
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

            if (!IsDesktopPreviewActive)
            {
                IsOpen = args.IsActive;
            }
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
            BeginDesktopPreview();
        }
    }

    private void HandleScrollerScrollStarted(object? sender, EventArgs args) => BeginDesktopPreview();

    private void HandleScrollerScrollStopped(object? sender, EventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            if (!dismissAfterPageNavigation)
            {
                return;
            }

            dismissAfterPageNavigation = false;
            DismissDesktopPreview();
        });

    private void BeginDesktopPreview()
    {
        if (!windowPreviewSurface.IsAvailable)
        {
            return;
        }

        scrollPresentationSession.Begin();

        dispatcher.Dispatch(() =>
        {
            IsDesktopPreviewCompletionRequested = false;
            IsDesktopPreviewActive = true;
            StaysOpen = true;
            IsOpen = true;
        });
    }
}
