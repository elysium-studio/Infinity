using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;
using NavigationCompletedEventArgs = Infinity.Application.Abstractions.NavigationCompletedEventArgs;
using NavigationStartedEventArgs = Infinity.Application.Abstractions.NavigationStartedEventArgs;

namespace Infinity.Shell;

public sealed partial class DesktopFlyoutViewModel :
    ObservableViewModel,
    IRecipient<NavigationStartedEventArgs>,
    IRecipient<NavigationCompletedEventArgs>,
    IRecipient<WindowActivationRequestedEventArgs>,
    IRecipient<OptionsChangedEventArgs<Settings>>,
    IRecipient<PointerScrollDeltaReceivedEventArgs>,
    IRecipient<PointerMiddleButtonClickedEventArgs>,
    IRecipient<ScrollerScrollStartedEventArgs>,
    IRecipient<WorkspaceLayoutChangedEventArgs>,
    IRecipient<WindowDragStartedEventArgs>,
    IRecipient<WindowDragStoppedEventArgs>
{
    private readonly IDispatcher dispatcher;
    private readonly IInfinityGlanceBridge glanceBridge;
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

    public DesktopFlyoutViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, IWorkspace workspace, IModifierKeyState modifierKeyState, IInfinityGlanceBridge glanceBridge, Settings settings) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.glanceBridge = glanceBridge;
        this.workspace = workspace;
        this.modifierKeyState = modifierKeyState;

        PreviewPosition = settings.PreviewPosition;
        Activate();
    }

    protected override void RegisterMessages()
    {
        Messenger.Register<NavigationStartedEventArgs>(this);
        Messenger.Register<NavigationCompletedEventArgs>(this);
        Messenger.Register<WindowActivationRequestedEventArgs>(this);
        Messenger.Register<OptionsChangedEventArgs<Settings>>(this);
        Messenger.Register<PointerScrollDeltaReceivedEventArgs>(this);
        Messenger.Register<PointerMiddleButtonClickedEventArgs>(this);
        Messenger.Register<ScrollerScrollStartedEventArgs>(this);
        Messenger.Register<WorkspaceLayoutChangedEventArgs>(this);
        Messenger.Register<WindowDragStartedEventArgs>(this);
        Messenger.Register<WindowDragStoppedEventArgs>(this);
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

    public void Receive(PointerScrollDeltaReceivedEventArgs args)
    {
        if (modifierKeyState.IsActive)
        {
            dispatcher.Dispatch(OpenOnCurrentWorkspace);
        }
    }

    public void Receive(PointerMiddleButtonClickedEventArgs args)
    {
        if (modifierKeyState.IsActive)
        {
            dispatcher.Dispatch(OpenOnCurrentWorkspace);
        }
    }

    public void Receive(ScrollerScrollStartedEventArgs args) =>
        dispatcher.Dispatch(() => userDismissed = false);

    public void Receive(WorkspaceLayoutChangedEventArgs args) =>
        dispatcher.Dispatch(() => currentWorkspace = workspace.GetCurrentWorkspace());

    public void Receive(WindowDragStartedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            userDismissed = false;
            StaysOpen = true;
            OpenOnCurrentWorkspace();
        });

    public void Receive(WindowDragStoppedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            StaysOpen = false;
            IsOpen = false;
        });

    partial void OnIsOpenChanged(bool value)
    {
        glanceBridge.SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface.DesktopFlyout, value);

        if (value)
        {
            currentWorkspace = workspace.GetCurrentWorkspace();
        }
        else
        {
            userDismissed = true;
            StaysOpen = false;
            Messenger.Send(new DesktopFlyoutClosedEventArgs());
        }
    }

    private void OpenOnCurrentWorkspace()
    {
        if (IsOpen && workspace.GetCurrentWorkspace() != currentWorkspace)
        {
            IsOpen = false;
        }

        IsOpen = true;
    }

    private void HandleScrollTick(object? sender, EventArgs args)
    {
        if (!userDismissed)
        {
            dispatcher.Dispatch(OpenOnCurrentWorkspace);
        }
    }
}
