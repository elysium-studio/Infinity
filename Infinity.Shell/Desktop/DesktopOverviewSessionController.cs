using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopOverviewSessionController
{
    private readonly IDispatcher dispatcher;
    private readonly IModifierKeyState modifierKeyState;
    private readonly IPager pager;
    private readonly IScroller scroller;
    private readonly IScrollPresentationSession scrollPresentationSession;
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly IWindowNavigationCoordinator windowNavigationCoordinator;
    private readonly IInfinityGlanceBridge glanceBridge;
    private readonly IDesktopOverviewSettingsNavigator settingsNavigator;
    private bool isDesktopPreviewExitAnimationCompleted;
    private bool isPageSelectionNavigationPending;
    private bool isWindowSelectionNavigationPending;
    private bool isSelectionNavigationStarting;
    private bool navigateToSettingsAfterClose;
    private bool isOpen;
    private bool staysOpen;
    private bool isPreviewActive;
    private bool isCompletionRequested;
    private bool isReadyToClose;

    public DesktopOverviewSessionController(IDispatcher dispatcher, IPointerInputSource pointer, IModifierKeyState modifierKeyState, IPageGestureSource gestureSource, IPager pager, IScroller scroller, IScrollPresentationSession scrollPresentationSession, IWindowPreviewSurface windowPreviewSurface, IWindowNavigationCoordinator windowNavigationCoordinator, IInfinityGlanceBridge glanceBridge, IDesktopOverviewSettingsNavigator settingsNavigator)
    {
        this.dispatcher = dispatcher;
        this.modifierKeyState = modifierKeyState;
        this.pager = pager;
        this.scroller = scroller;
        this.scrollPresentationSession = scrollPresentationSession;
        this.windowPreviewSurface = windowPreviewSurface;
        this.windowNavigationCoordinator = windowNavigationCoordinator;
        this.glanceBridge = glanceBridge;
        this.settingsNavigator = settingsNavigator;
        pointer.ScrollDeltaReceived += HandleScrollDeltaReceived;
        pointer.MiddleButtonClicked += HandleMiddleButtonClicked;
        scroller.ScrollStarted += HandleScrollerScrollStarted;
        scroller.ScrollStopped += HandleScrollerScrollStopped;
        gestureSource.SessionStarted += HandleGestureSessionStarted;
        gestureSource.SessionEnded += HandleGestureSessionEnded;
    }


    public event Action<DesktopOverviewSessionState>? StateChanged;

    public DesktopOverviewSessionState State => new(isOpen, staysOpen, isPreviewActive, isCompletionRequested, isReadyToClose);

    public void SetOpen(bool value)
    {
        if (isOpen == value)
        {
            return;
        }

        isOpen = value;
        if (!value && isPreviewActive)
        {
            scroller.CommitPresentation();
            scrollPresentationSession.End();
            isPreviewActive = false;
            isCompletionRequested = false;
            isReadyToClose = false;
            ResetPendingNavigation();
            navigateToSettingsAfterClose = false;
        }

        PublishState();
    }


    public void SetStaysOpen(bool value)
    {
        if (staysOpen == value)
        {
            return;
        }

        staysOpen = value;
        PublishState();
    }


    public void CompletePreview()
    {
        bool navigateToSettings = navigateToSettingsAfterClose;
        ResetPendingNavigation();
        navigateToSettingsAfterClose = false;
        isReadyToClose = false;
        scroller.CommitPresentation();
        scrollPresentationSession.End();
        isPreviewActive = false;
        isCompletionRequested = false;
        staysOpen = false;
        isOpen = false;
        PublishState();
        if (navigateToSettings)
        {
            _ = settingsNavigator.NavigateAsync();
        }
    }


    public void DismissPreview()
    {
        if (!isPreviewActive || isCompletionRequested)
        {
            return;
        }

        scroller.Reset();
        scroller.CommitPresentation();
        isDesktopPreviewExitAnimationCompleted = false;
        isPageSelectionNavigationPending = false;
        isWindowSelectionNavigationPending = false;
        isReadyToClose = false;
        isCompletionRequested = true;
        PublishState();
    }


#if DEBUG
    public async Task OpenForDebugAsync()
    {
        staysOpen = true;
        isOpen = true;
        PublishState();
        await Task.Delay(500);
        dispatcher.Dispatch(BeginPreview);
    }


#endif
    public void ActivateWindow(nint handle)
    {
        if (handle == 0 || !isPreviewActive || isCompletionRequested)
        {
            return;
        }

        navigateToSettingsAfterClose = false;
        isDesktopPreviewExitAnimationCompleted = false;
        isPageSelectionNavigationPending = false;
        isWindowSelectionNavigationPending = false;
        isReadyToClose = false;
        isCompletionRequested = true;
        isSelectionNavigationStarting = true;
        PublishState();
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
        if (!isPreviewActive || isCompletionRequested)
        {
            return;
        }

        navigateToSettingsAfterClose = false;
        if (pager.IsPageCentered(page))
        {
            DismissPreview();
            return;
        }

        isDesktopPreviewExitAnimationCompleted = false;
        isPageSelectionNavigationPending = true;
        isWindowSelectionNavigationPending = false;
        isReadyToClose = false;
        isCompletionRequested = true;
        isSelectionNavigationStarting = true;
        PublishState();
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
        if (!isPreviewActive || isCompletionRequested)
        {
            return;
        }

        navigateToSettingsAfterClose = true;
        DismissPreview();
    }


    public void NotifyNavigationCompleted() => dispatcher.Dispatch(() =>
    {
        isWindowSelectionNavigationPending = false;
        TryMarkReadyToClose();
        if (!isPreviewActive)
        {
            isOpen = false;
        }

        PublishState();
    });

    public void NotifyWindowActivationRequested() => dispatcher.Dispatch(() =>
    {
        if (!isPreviewActive)
        {
            isOpen = false;
            PublishState();
        }
    });

    public void NotifyExitAnimationCompleted()
    {
        if (!isCompletionRequested)
        {
            return;
        }

        isDesktopPreviewExitAnimationCompleted = true;
        TryMarkReadyToClose();
        PublishState();
    }


    private void HandleGestureSessionStarted() => dispatcher.Dispatch(OpenPendingSurface);

    private void HandleGestureSessionEnded() => dispatcher.Dispatch(ClosePendingSurface);

    private void HandleScrollDeltaReceived(int delta)
    {
        if (!modifierKeyState.IsActive)
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            if (isCompletionRequested)
            {
                ResumePreview();
            }
            else
            {
                isOpen = true;
                PublishState();
            }
        });
    }


    private void HandleMiddleButtonClicked()
    {
        if (modifierKeyState.IsActive)
        {
            BeginPreview();
        }
    }


    private void HandleScrollerScrollStarted(object? sender, EventArgs args)
    {
        if (!isSelectionNavigationStarting)
        {
            BeginPreview();
        }
    }


    private void HandleScrollerScrollStopped(object? sender, EventArgs args) => dispatcher.Dispatch(() =>
    {
        if (!isPageSelectionNavigationPending)
        {
            return;
        }

        isPageSelectionNavigationPending = false;
        TryMarkReadyToClose();
        PublishState();
    });

    private void BeginPreview()
    {
        if (!windowPreviewSurface.IsAvailable)
        {
            return;
        }

        if (isPreviewActive && isCompletionRequested)
        {
            ResumePreview();
            return;
        }

        if (!isPreviewActive)
        {
            ResetPendingNavigation();
            isReadyToClose = false;
        }

        scrollPresentationSession.Begin();
        dispatcher.Dispatch(() =>
        {
            navigateToSettingsAfterClose = false;
            isCompletionRequested = false;
            isPreviewActive = true;
            staysOpen = true;
            isOpen = true;
            PublishState();
        });
    }


    private void ResumePreview()
    {
        if (!isPreviewActive || !isCompletionRequested)
        {
            return;
        }

        ResetPendingNavigation();
        navigateToSettingsAfterClose = false;
        isReadyToClose = false;
        windowNavigationCoordinator.CancelNavigation();
        scroller.CancelNavigation();
        isCompletionRequested = false;
        staysOpen = true;
        isOpen = true;
        PublishState();
    }


    private void OpenPendingSurface()
    {
        staysOpen = true;
        isOpen = true;
        PublishState();
    }


    private void ClosePendingSurface()
    {
        if (isPreviewActive)
        {
            return;
        }

        staysOpen = false;
        isOpen = false;
        PublishState();
    }


    private void TryMarkReadyToClose()
    {
        if (isCompletionRequested && isDesktopPreviewExitAnimationCompleted && !isPageSelectionNavigationPending && !isWindowSelectionNavigationPending)
        {
            isReadyToClose = true;
        }
    }


    private void ResetPendingNavigation()
    {
        isDesktopPreviewExitAnimationCompleted = false;
        isPageSelectionNavigationPending = false;
        isWindowSelectionNavigationPending = false;
        isSelectionNavigationStarting = false;
    }


    private void PublishState()
    {
        glanceBridge.SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface.DesktopOverview, isOpen);
        StateChanged?.Invoke(State);
    }
}
