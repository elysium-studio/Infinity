using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Infinity.Application.Abstractions;
using NavigationCompletedEventArgs = Infinity.Application.Abstractions.NavigationCompletedEventArgs;

namespace Infinity.Shell;

public sealed partial class DesktopOverviewViewModel :
    ObservableViewModel,
    IRecipient<NavigationCompletedEventArgs>,
    IRecipient<WindowActivationRequestedEventArgs>
{
    private readonly DesktopOverviewSessionController sessionController;
    private bool isApplyingSessionState;

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

    public DesktopOverviewViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, DesktopOverviewSessionController sessionController) : base(provider, factory, messenger, disposer)
    {
        this.sessionController = sessionController;
        sessionController.StateChanged += HandleSessionStateChanged;
        ApplySessionState(sessionController.State);
        Activate();
    }

    partial void OnIsOpenChanged(bool value)
    {
        if (!isApplyingSessionState)
        {
            sessionController.SetOpen(value);
        }
    }

    partial void OnStaysOpenChanged(bool value)
    {
        if (!isApplyingSessionState)
        {
            sessionController.SetStaysOpen(value);
        }
    }

    protected override void RegisterMessages()
    {
        Messenger.Register<NavigationCompletedEventArgs>(this);
        Messenger.Register<WindowActivationRequestedEventArgs>(this);
    }

    public void CompleteDesktopPreview() => sessionController.CompletePreview();

    public void DismissDesktopPreview() => sessionController.DismissPreview();

#if DEBUG
    public Task OpenDesktopPreviewForDebugAsync() => sessionController.OpenForDebugAsync();
#endif

    public void ActivateWindow(nint handle) => sessionController.ActivateWindow(handle);

    public void SelectPage(int page) => sessionController.SelectPage(page);

    public void NavigateToSettings() => sessionController.NavigateToSettings();

    public void NotifyDesktopPreviewExitAnimationCompleted() => sessionController.NotifyExitAnimationCompleted();

    public void Receive(NavigationCompletedEventArgs args) => sessionController.NotifyNavigationCompleted();

    public void Receive(WindowActivationRequestedEventArgs args) => sessionController.NotifyWindowActivationRequested();

    private void HandleSessionStateChanged(DesktopOverviewSessionState state) => ApplySessionState(state);

    private void ApplySessionState(DesktopOverviewSessionState state)
    {
        isApplyingSessionState = true;

        try
        {
            IsOpen = state.IsOpen;
            StaysOpen = state.StaysOpen;
            IsDesktopPreviewActive = state.IsPreviewActive;
            IsDesktopPreviewCompletionRequested = state.IsCompletionRequested;
            IsDesktopPreviewReadyToClose = state.IsReadyToClose;
        }
        finally
        {
            isApplyingSessionState = false;
        }
    }
}
