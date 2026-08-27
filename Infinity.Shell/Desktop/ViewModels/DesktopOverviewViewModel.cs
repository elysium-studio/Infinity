using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using NavigationCompletedEventArgs = Infinity.Application.Abstractions.NavigationCompletedEventArgs;

namespace Infinity.Shell;

public sealed partial class DesktopOverviewViewModel :
    ObservableViewModel,
    IRecipient<NavigationCompletedEventArgs>,
    IRecipient<WindowActivationRequestedEventArgs>
{
    private readonly IDispatcher dispatcher;
    private readonly IModifierKeyState modifierKeyState;
    private readonly IPager pager;
    private readonly IScroller scroller;
    private readonly IScrollPresentationSession scrollPresentationSession;
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly IWindowNavigationCoordinator windowNavigationCoordinator;
    private readonly IInfinityGlanceBridge glanceBridge;
    private readonly INavigator navigator;
    private readonly ILogger<DesktopOverviewViewModel> logger;
    private bool isDesktopPreviewExitAnimationCompleted;
    private bool isPageSelectionNavigationPending;
    private bool isWindowSelectionNavigationPending;
    private bool isSelectionNavigationStarting;
    private bool navigateToSettingsAfterClose;

    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private bool staysOpen;

    [ObservableProperty]
    private bool isDesktopPreviewActive;

    [ObservableProperty]
    private bool isDesktopPreviewCompletionRequested;

    [ObservableProperty]
    private bool isDesktopPreviewReadyToClose;

    partial void OnIsOpenChanged(bool value)
    {
        glanceBridge.SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface.DesktopOverview, value);

        if (!value && IsDesktopPreviewActive)
        {
            scroller.CommitPresentation();
            scrollPresentationSession.End();
            IsDesktopPreviewActive = false;
            IsDesktopPreviewCompletionRequested = false;
            IsDesktopPreviewReadyToClose = false;
            isDesktopPreviewExitAnimationCompleted = false;
            isPageSelectionNavigationPending = false;
            isWindowSelectionNavigationPending = false;
            isSelectionNavigationStarting = false;
            navigateToSettingsAfterClose = false;
        }
    }

    public DesktopOverviewViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, IPointerInputSource pointer, IModifierKeyState modifierKeyState, IWindowDragScroller dragScroller, IPageGestureSource gestureSource, IPager pager, IScroller scroller, IScrollPresentationSession scrollPresentationSession, IWindowPreviewSurface windowPreviewSurface, IWindowNavigationCoordinator windowNavigationCoordinator, IInfinityGlanceBridge glanceBridge, INavigator navigator, ILogger<DesktopOverviewViewModel> logger) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.modifierKeyState = modifierKeyState;
        this.pager = pager;
        this.scroller = scroller;
        this.scrollPresentationSession = scrollPresentationSession;
        this.windowPreviewSurface = windowPreviewSurface;
        this.windowNavigationCoordinator = windowNavigationCoordinator;
        this.glanceBridge = glanceBridge;
        this.navigator = navigator;
        this.logger = logger;

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
    }

    public void CompleteDesktopPreview()
    {
        bool navigateToSettings = navigateToSettingsAfterClose;

        isDesktopPreviewExitAnimationCompleted = false;
        isPageSelectionNavigationPending = false;
        isWindowSelectionNavigationPending = false;
        navigateToSettingsAfterClose = false;
        IsDesktopPreviewReadyToClose = false;
        scroller.CommitPresentation();
        scrollPresentationSession.End();
        IsDesktopPreviewActive = false;
        IsDesktopPreviewCompletionRequested = false;
        StaysOpen = false;
        IsOpen = false;

        if (navigateToSettings)
        {
            _ = NavigateToSettingsAsync();
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
        isDesktopPreviewExitAnimationCompleted = false;
        isPageSelectionNavigationPending = false;
        isWindowSelectionNavigationPending = false;
        IsDesktopPreviewReadyToClose = false;
        IsDesktopPreviewCompletionRequested = true;
    }

#if DEBUG
    public async Task OpenDesktopPreviewForDebugAsync()
    {
        StaysOpen = true;
        IsOpen = true;

        await Task.Delay(500);
        dispatcher.Dispatch(BeginDesktopPreview);
    }
#endif

    public void ActivateWindow(nint handle)
    {
        if (handle == 0 || !IsDesktopPreviewActive || IsDesktopPreviewCompletionRequested)
        {
            return;
        }

        navigateToSettingsAfterClose = false;
        isDesktopPreviewExitAnimationCompleted = false;
        isPageSelectionNavigationPending = false;
        isWindowSelectionNavigationPending = false;
        IsDesktopPreviewReadyToClose = false;
        IsDesktopPreviewCompletionRequested = true;
        isSelectionNavigationStarting = true;

        try
        {
            windowNavigationCoordinator.NavigateTo(handle);
            isWindowSelectionNavigationPending = windowNavigationCoordinator.NavigationTargetPage >= 0;
        }
        finally
        {
            isSelectionNavigationStarting = false;
        }
    }

    public void SelectPage(int page)
    {
        if (!IsDesktopPreviewActive || IsDesktopPreviewCompletionRequested)
        {
            return;
        }

        navigateToSettingsAfterClose = false;

        if (pager.IsPageCentered(page))
        {
            DismissDesktopPreview();
            return;
        }

        isDesktopPreviewExitAnimationCompleted = false;
        isPageSelectionNavigationPending = true;
        isWindowSelectionNavigationPending = false;
        IsDesktopPreviewReadyToClose = false;
        IsDesktopPreviewCompletionRequested = true;
        isSelectionNavigationStarting = true;

        try
        {
            pager.NavigateToPage(page);
        }
        finally
        {
            isSelectionNavigationStarting = false;
        }
    }

    public void NavigateToSettings()
    {
        if (!IsDesktopPreviewActive || IsDesktopPreviewCompletionRequested)
        {
            return;
        }

        navigateToSettingsAfterClose = true;
        DismissDesktopPreview();
    }

    public void Receive(NavigationCompletedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            isWindowSelectionNavigationPending = false;
            TryMarkDesktopPreviewReadyToClose();

            if (!IsDesktopPreviewActive)
            {
                IsOpen = false;
            }
        });

    public void Receive(WindowActivationRequestedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            if (!IsDesktopPreviewActive)
            {
                IsOpen = false;
            }
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
                if (IsDesktopPreviewCompletionRequested)
                {
                    ResumeDesktopPreview();
                }
                else
                {
                    IsOpen = true;
                }
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

    public void NotifyDesktopPreviewExitAnimationCompleted()
    {
        if (!IsDesktopPreviewCompletionRequested)
        {
            return;
        }

        isDesktopPreviewExitAnimationCompleted = true;
        TryMarkDesktopPreviewReadyToClose();
    }

    private void HandleScrollerScrollStarted(object? sender, EventArgs args)
    {
        if (!isSelectionNavigationStarting)
        {
            BeginDesktopPreview();
        }
    }

    private void HandleScrollerScrollStopped(object? sender, EventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            if (!isPageSelectionNavigationPending)
            {
                return;
            }

            isPageSelectionNavigationPending = false;
            TryMarkDesktopPreviewReadyToClose();
        });

    private void BeginDesktopPreview()
    {
        if (!windowPreviewSurface.IsAvailable)
        {
            return;
        }

        if (IsDesktopPreviewActive && IsDesktopPreviewCompletionRequested)
        {
            ResumeDesktopPreview();
            return;
        }

        if (!IsDesktopPreviewActive)
        {
            isDesktopPreviewExitAnimationCompleted = false;
            isPageSelectionNavigationPending = false;
            isWindowSelectionNavigationPending = false;
            IsDesktopPreviewReadyToClose = false;
        }

        scrollPresentationSession.Begin();

        dispatcher.Dispatch(() =>
        {
            navigateToSettingsAfterClose = false;
            IsDesktopPreviewCompletionRequested = false;
            IsDesktopPreviewActive = true;
            StaysOpen = true;
            IsOpen = true;
        });
    }

    private void ResumeDesktopPreview()
    {
        if (!IsDesktopPreviewActive || !IsDesktopPreviewCompletionRequested)
        {
            return;
        }

        isDesktopPreviewExitAnimationCompleted = false;
        isPageSelectionNavigationPending = false;
        isWindowSelectionNavigationPending = false;
        navigateToSettingsAfterClose = false;
        IsDesktopPreviewReadyToClose = false;
        windowNavigationCoordinator.CancelNavigation();
        scroller.CancelNavigation();
        IsDesktopPreviewCompletionRequested = false;
        StaysOpen = true;
        IsOpen = true;
    }

    private void TryMarkDesktopPreviewReadyToClose()
    {
        if (IsDesktopPreviewCompletionRequested &&
            isDesktopPreviewExitAnimationCompleted &&
            !isPageSelectionNavigationPending &&
            !isWindowSelectionNavigationPending)
        {
            IsDesktopPreviewReadyToClose = true;
        }
    }

    private async Task NavigateToSettingsAsync()
    {
        try
        {
            await navigator.NavigateAsync("SettingsWindow");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to navigate to Settings");
        }
    }
}
