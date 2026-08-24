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
    private readonly IScroller scroller;
    private readonly IScrollPresentationSession scrollPresentationSession;
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly IInfinityGlanceBridge glanceBridge;
    private bool filterActive;
    private bool scrollMotionActive;

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
        glanceBridge.SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface.PageTint, value);

        if (!value && IsDesktopPreviewActive)
        {
            scroller.CommitPresentation();
            scrollPresentationSession.End();
            IsDesktopPreviewActive = false;
            IsDesktopPreviewCompletionRequested = false;
        }
    }

    public PageTintViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, IPointerInputSource pointer, IModifierKeyState modifierKeyState, IWindowDragScroller dragScroller, IPageGestureSource gestureSource, IOptionsMonitor<Settings> settings, IScroller scroller, IScrollPresentationSession scrollPresentationSession, IWindowPreviewSurface windowPreviewSurface, IInfinityGlanceBridge glanceBridge) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.modifierKeyState = modifierKeyState;
        this.settings = settings;
        this.scroller = scroller;
        this.scrollPresentationSession = scrollPresentationSession;
        this.windowPreviewSurface = windowPreviewSurface;
        this.glanceBridge = glanceBridge;

        isBlurEnabled = settings.CurrentValue.DesktopBlur;

        pointer.ScrollDeltaReceived += HandleScrollDeltaReceived;
        pointer.MiddleButtonClicked += HandleMiddleButtonClicked;
        modifierKeyState.StateChanged += HandleModifierKeyStateChanged;
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
}
