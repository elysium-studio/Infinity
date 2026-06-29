using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using NavigationCompletedEventArgs = Infinity.Application.Abstractions.NavigationCompletedEventArgs;
using NavigationStartedEventArgs = Infinity.Application.Abstractions.NavigationStartedEventArgs;

namespace Infinity.Shell;

public partial class DesktopFlyoutViewModel :
    ObservableViewModel,
    IRecipient<NavigationStartedEventArgs>,
    IRecipient<NavigationCompletedEventArgs>,
    IRecipient<WindowActivationRequestedEventArgs>,
    IRecipient<OptionsChangedEventArgs<Settings>>
{
    private readonly IDispatcher dispatcher;
    private readonly IWorkspace workspace;
    private readonly IModifierKeyState modifierKeyState;

    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private bool staysOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacementIndex))]
    private PreviewPosition previewPosition;

    public int PlacementIndex => (int)PreviewPosition;

    private bool userDismissed;
    private IntPtr currentWorkspace;

    public DesktopFlyoutViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IDispatcher dispatcher,
        IScroller scroller,
        IWorkspace workspace,
        IPointerInputSource pointer,
        IModifierKeyState modifierKeyState,
        IWindowDragScroller dragScroller,
        Settings settings) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.workspace = workspace;
        this.modifierKeyState = modifierKeyState;
        PreviewPosition = settings.PreviewPosition;
        pointer.ScrollDeltaReceived += HandleScrollDeltaReceived;
        pointer.MiddleButtonClicked += HandleMiddleButtonClicked;
        scroller.ScrollStarted += HandleScrollStarted;
        workspace.WorkspaceLayoutChanged += HandleWorkspaceLayoutChanged;
        dragScroller.DragStarted += HandleDragStarted;
        dragScroller.DragStopped += HandleDragStopped;
        IsActive = true;
    }

    public void Receive(NavigationStartedEventArgs args)
    {
        dispatcher.Dispatch(() =>
        {
            IsOpen = true;
            StaysOpen = true;
        });
    }

    public void Receive(NavigationCompletedEventArgs args) =>
        dispatcher.Dispatch(() => StaysOpen = false);

    public void Receive(WindowActivationRequestedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            StaysOpen = false;
            IsOpen = false;
        });

    public void Receive(OptionsChangedEventArgs<Settings> message) =>
        dispatcher.Dispatch(() => PreviewPosition = message.Options.PreviewPosition);

    partial void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            currentWorkspace = workspace.GetCurrentWorkspace();
        }
        else
        {
            userDismissed = true;
            StaysOpen = false;
        }
    }

    private void HandleScrollDeltaReceived(int delta)
    {
        if (modifierKeyState.IsActive)
        {
            dispatcher.Dispatch(OpenOnCurrentWorkspace);
        }
    }

    private void HandleMiddleButtonClicked()
    {
        if (modifierKeyState.IsActive)
        {
            dispatcher.Dispatch(OpenOnCurrentWorkspace);
        }
    }

    private void HandleDragStarted() =>
        dispatcher.Dispatch(() =>
        {
            userDismissed = false;
            StaysOpen = true;
            OpenOnCurrentWorkspace();
        });

    private void HandleDragStopped() =>
        dispatcher.Dispatch(() =>
        {
            StaysOpen = false;
            IsOpen = false;
        });

    private void OpenOnCurrentWorkspace()
    {
        if (IsOpen && workspace.GetCurrentWorkspace() != currentWorkspace)
        {
            IsOpen = false;
        }

        IsOpen = true;
    }

    private void HandleScrollStarted(object? sender, EventArgs args)
    {
        userDismissed = false;
    }

    private void HandleScrollTick(object? sender, EventArgs args)
    {
        if (!userDismissed)
        {
            dispatcher.Dispatch(OpenOnCurrentWorkspace);
        }
    }

    private void HandleWorkspaceLayoutChanged(object? sender, EventArgs args) =>
        currentWorkspace = workspace.GetCurrentWorkspace();
}