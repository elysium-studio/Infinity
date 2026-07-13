using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Infinity.Platform.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace Infinity.Shell.WinUI;

public class ShellModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddSingleton<IKeyboardInputKeysFactory>(provider => new KeyboardInputKeysFactory(() =>
                provider.GetRequiredService<Settings>().ScrollModifierKeys is { Count: > 0 } keys
                    ? keys
                    : [[VirtualKeys.VK_LWIN, VirtualKeys.VK_RWIN], [VirtualKeys.VK_LCONTROL, VirtualKeys.VK_RCONTROL]]))
            .AddSingleton<IShellLayoutCalculator, ShellLayoutCalculator>()
            .AddSingleton<IScrollTimer, DwmFlushScrollTimer>()
            .AddSingleton<IPager>(provider => new Pager(
                provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IPanState>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWorkspace>(),
                provider.GetRequiredService<ILogger<Pager>>()));

        services.Subscribe<IPointerInputSource>((provider, pointer) =>
        {
            IMessenger messenger = provider.GetRequiredService<IMessenger>();

            void HandleScrollDeltaReceived(int delta) =>
                messenger.Send(new PointerScrollDeltaReceivedEventArgs(delta));

            void HandleMiddleButtonClicked() =>
                messenger.Send(new PointerMiddleButtonClickedEventArgs());

            pointer.ScrollDeltaReceived += HandleScrollDeltaReceived;
            pointer.MiddleButtonClicked += HandleMiddleButtonClicked;

            return () =>
            {
                pointer.ScrollDeltaReceived -= HandleScrollDeltaReceived;
                pointer.MiddleButtonClicked -= HandleMiddleButtonClicked;
            };
        });

        services.Subscribe<IScroller>((provider, scroller) =>
        {
            IMessenger messenger = provider.GetRequiredService<IMessenger>();

            void HandleScrollStarted(object? sender, EventArgs args) =>
                messenger.Send(new ScrollerScrollStartedEventArgs());

            scroller.ScrollStarted += HandleScrollStarted;

            return () => scroller.ScrollStarted -= HandleScrollStarted;
        });

        services.Subscribe<IWorkspace>((provider, workspace) =>
        {
            IMessenger messenger = provider.GetRequiredService<IMessenger>();

            void HandleWorkspaceLayoutChanged(object? sender, EventArgs args) =>
                messenger.Send(new WorkspaceLayoutChangedEventArgs());

            workspace.WorkspaceLayoutChanged += HandleWorkspaceLayoutChanged;

            return () => workspace.WorkspaceLayoutChanged -= HandleWorkspaceLayoutChanged;
        });

        services.Subscribe<IWindowDragScroller>((provider, dragScroller) =>
        {
            IMessenger messenger = provider.GetRequiredService<IMessenger>();

            void HandleDragStarted() =>
                messenger.Send(new WindowDragStartedEventArgs());

            void HandleDragStopped() =>
                messenger.Send(new WindowDragStoppedEventArgs());

            void HandleDragMoved() =>
                messenger.Send(new WindowDragMovedEventArgs());

            void HandleDragScrolled() =>
                messenger.Send(new WindowDragScrolledEventArgs());

            dragScroller.DragStarted += HandleDragStarted;
            dragScroller.DragStopped += HandleDragStopped;
            dragScroller.DragMoved += HandleDragMoved;
            dragScroller.DragScrolled += HandleDragScrolled;

            return () =>
            {
                dragScroller.DragStarted -= HandleDragStarted;
                dragScroller.DragStopped -= HandleDragStopped;
                dragScroller.DragMoved -= HandleDragMoved;
                dragScroller.DragScrolled -= HandleDragScrolled;
            };
        });

        services.Subscribe<IThumbnailDragScroller>((provider, dragScroller) =>
        {
            IMessenger messenger = provider.GetRequiredService<IMessenger>();

            void HandleScrolled() =>
                messenger.Send(new WindowDragScrolledEventArgs());

            dragScroller.Scrolled += HandleScrolled;

            return () => dragScroller.Scrolled -= HandleScrolled;
        });
    }
}
