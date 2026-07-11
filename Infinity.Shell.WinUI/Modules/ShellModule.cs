using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Elysium.Presentation.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Infinity.Platform.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using IApplicationLifetime = Elysium.Application.Abstractions.IApplicationLifetime;

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
                provider.GetRequiredService<ILogger<Pager>>()))
            .AddSingleton<IWindowFilterState, TrackedWindowFilter>()
            .AddSingleton<IWindowPeekSource, WindowPeekSource>()
            .AddSingleton<IPeekSource>(provider =>
                provider.GetRequiredService<IWindowPeekSource>())
            .AddSingleton<IPeekSource>(provider =>
                new FilterPeekSource(provider.GetRequiredService<IWindowFilterState>()))
            .AddSingleton<IWindowPeekController>(provider => new WindowPeekController(
                provider.GetRequiredService<IWindowStore>(),
                provider.GetServices<IPeekSource>(),
                provider.GetRequiredService<IWindowConcealer>(),
                provider.GetRequiredService<IScroller>(),
                () => provider.GetRequiredService<IOptionsMonitor<Settings>>().CurrentValue.HideFilteredWindows))
            .AddSingleton<ITrackedWindowCollection, TrackedWindowCollection>()
            .AddSingleton<IDesktopBackgroundController>(provider => new DesktopBackgroundController(
                provider.GetRequiredService<IDesktopBackgroundSource>(),
                provider.GetRequiredService<IDispatcher>()))
            .AddSingleton<WindowCollection>(provider => new WindowCollection(
                provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IScrollTimer>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWindowStack>(),
                provider.GetRequiredService<IForegroundWindowTracker>(),
                provider.GetRequiredService<IWindowEventListener>(),
                provider.GetRequiredService<IWorkspace>(),
                provider.GetRequiredService<IWindowFilterState>(),
                provider.GetRequiredService<IForegroundWindowCoordinator>(),
                provider.GetRequiredService<ITrackedWindowCollection>(),
                provider.GetRequiredService<IDispatcher>(),
                provider.GetRequiredService<ILogger<WindowCollection>>()))
            .AddSingleton<IWindowCollection>(provider =>
                provider.GetRequiredService<WindowCollection>())
            .AddSingleton<IWindowCollectionLifetime>(provider =>
                provider.GetRequiredService<WindowCollection>())
            .RegisterFactory((provider, factoryArgs) => new TrackedWindowViewModel(
                provider,
                provider.GetRequiredService<IServiceFactory>(),
                provider.GetRequiredService<IMessenger>(),
                provider.GetRequiredService<IDisposer>(),
                provider.GetRequiredService<IWindowController>(),
                provider.GetRequiredService<IWindowPreviewSurface>(),
                (IntPtr)factoryArgs![0]!))
            .AddViewFor(
                ServiceLifetime.Singleton,
                provider => new PageTintView(
                    provider.GetRequiredService<IMonitorLocator>(),
                    provider.GetRequiredService<ITaskbarLocator>()),
                provider => new PageTintViewModel(
                    provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IPointerInputSource>(),
                    provider.GetRequiredService<IModifierKeyState>(),
                    provider.GetRequiredService<IWindowDragScroller>(),
                    provider.GetRequiredService<IPageGestureSource>(),
                    provider.GetRequiredService<IOptionsMonitor<Settings>>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    provider.GetRequiredService<IPager>(),
                    provider.GetRequiredService<IPanState>()))
            .AddViewFor(
                ServiceLifetime.Singleton,
                provider => new DesktopFlyoutView(
                    provider.GetRequiredService<IWindowPreviewSurface>()),
                provider => new DesktopFlyoutViewModel(
                    provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IWorkspace>(),
                    provider.GetRequiredService<IModifierKeyState>(),
                    provider.GetRequiredService<Settings>()))
            .AddView(
                ServiceLifetime.Singleton,
                provider => new ScrollTriggerView())
            .AddViewFor(
                ServiceLifetime.Singleton,
                provider => new TrackedWindowCollectionView(),
                provider => new TrackedWindowCollectionViewModel(
                    provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IWorkspace>(),
                    provider.GetRequiredService<IShellLayoutCalculator>(),
                    provider.GetRequiredService<IPager>(),
                    provider.GetRequiredService<IPanState>(),
                    provider.GetRequiredService<IScroller>(),
                    provider.GetRequiredService<IWindowCollection>(),
                    provider.GetRequiredService<ITrackedWindowCollection>(),
                    provider.GetRequiredService<IWindowSelector>(),
                    provider.GetRequiredService<IWindowFilterState>(),
                    provider.GetRequiredService<IWindowPeekController>(),
                    provider.GetRequiredService<IWindowPeekSource>(),
                    provider.GetRequiredService<IDesktopBackgroundController>(),
                    provider.GetRequiredService<IWindowNavigationCoordinator>(),
                    provider.GetRequiredService<INavigator>(),
                    provider.GetRequiredService<IOptionsMonitor<Settings>>(),
                    provider.GetRequiredService<IApplicationLifetime>(),
                    provider.GetRequiredService<ILogger<TrackedWindowCollectionViewModel>>()))
            .AddViewFor<TrackedWindowView, TrackedWindowViewModel>(
                ServiceLifetime.Transient,
                provider => new TrackedWindowView());

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

            return () =>
                workspace.WorkspaceLayoutChanged -= HandleWorkspaceLayoutChanged;
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

        services.Subscribe<IWindowCollection>((provider, windowCollection) =>
        {
            IMessenger messenger = provider.GetRequiredService<IMessenger>();

            void HandleWindowAdded(object? sender, TrackedWindow trackedWindow) =>
                messenger.Send(new TrackedWindowAddedEventArgs(trackedWindow));

            void HandleWindowRemoved(object? sender, IntPtr handle) =>
                messenger.Send(new TrackedWindowRemovedEventArgs(handle));

            void HandleWindowChanged(object? sender, TrackedWindow trackedWindow) =>
                messenger.Send(new TrackedWindowChangedEventArgs(trackedWindow));

            void HandleWindowStackRefreshed(object? sender, EventArgs args) =>
                messenger.Send(new WindowStackRefreshedEventArgs());

            void HandleWorkspaceLayoutChanged(object? sender, EventArgs args) =>
                messenger.Send(new WindowCollectionWorkspaceLayoutChangedEventArgs());

            void HandleRefreshRequested(object? sender, EventArgs args) =>
                messenger.Send(new WindowCollectionRefreshRequestedEventArgs());

            windowCollection.WindowAdded += HandleWindowAdded;
            windowCollection.WindowRemoved += HandleWindowRemoved;
            windowCollection.WindowChanged += HandleWindowChanged;
            windowCollection.WindowStackRefreshed += HandleWindowStackRefreshed;
            windowCollection.WorkspaceLayoutChanged += HandleWorkspaceLayoutChanged;
            windowCollection.RefreshRequested += HandleRefreshRequested;

            return () =>
            {
                windowCollection.WindowAdded -= HandleWindowAdded;
                windowCollection.WindowRemoved -= HandleWindowRemoved;
                windowCollection.WindowChanged -= HandleWindowChanged;
                windowCollection.WindowStackRefreshed -= HandleWindowStackRefreshed;
                windowCollection.WorkspaceLayoutChanged -= HandleWorkspaceLayoutChanged;
                windowCollection.RefreshRequested -= HandleRefreshRequested;
            };
        });

        services.Subscribe<IDesktopBackgroundController>((provider, backgroundController) =>
        {
            IMessenger messenger = provider.GetRequiredService<IMessenger>();

            void HandleBackgroundChanged(object? sender, EventArgs args) =>
                messenger.Send(new DesktopBackgroundChangedEventArgs());

            backgroundController.BackgroundChanged += HandleBackgroundChanged;

            return () =>
                backgroundController.BackgroundChanged -= HandleBackgroundChanged;
        });
    }
}
