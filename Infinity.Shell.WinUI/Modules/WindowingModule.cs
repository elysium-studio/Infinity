using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace Infinity.Shell.WinUI;

public class WindowingModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddSingleton<IWindowFilterState, TrackedWindowFilter>()
            .AddSingleton<IWindowPeekSource, global::Infinity.Application.WindowPeekSource>()
            .AddSingleton<IPeekSource>(provider => provider.GetRequiredService<IWindowPeekSource>())
            .AddSingleton<IPeekSource>(provider => new global::Infinity.Application.FilterPeekSource(
                provider.GetRequiredService<IWindowFilterState>()))
            .AddSingleton<IWindowPeekController>(provider => new global::Infinity.Application.WindowPeekController(
                provider.GetRequiredService<IWindowStore>(),
                provider.GetServices<IPeekSource>(),
                provider.GetRequiredService<IWindowConcealer>(),
                provider.GetRequiredService<IScroller>(),
                () => provider.GetRequiredService<IOptionsMonitor<Settings>>().CurrentValue.HideFilteredWindows))
            .AddSingleton<ITrackedWindowCollection, TrackedWindowCollection>()
            .AddSingleton<WindowCollection>(provider => new WindowCollection(
                provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IScrollTimer>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWindowStack>(),
                provider.GetRequiredService<IForegroundWindowTracker>(),
                provider.GetRequiredService<global::Elysium.Platform.Abstractions.IWindowEventListener>(),
                provider.GetRequiredService<global::Elysium.Platform.Abstractions.IWorkspace>(),
                provider.GetRequiredService<IWindowFilterState>(),
                provider.GetRequiredService<IForegroundWindowCoordinator>(),
                provider.GetRequiredService<IDispatcher>(),
                provider.GetRequiredService<ILogger<WindowCollection>>()))
            .AddSingleton<IWindowCollection>(provider => provider.GetRequiredService<WindowCollection>())
            .AddSingleton<IWindowCollectionLifetime>(provider => provider.GetRequiredService<WindowCollection>())
            .RegisterFactory((provider, factoryArgs) => new TrackedWindowViewModel(
                provider,
                provider.GetRequiredService<IServiceFactory>(),
                provider.GetRequiredService<IMessenger>(),
                provider.GetRequiredService<IDisposer>(),
                provider.GetRequiredService<IWindowController>(),
                provider.GetRequiredService<IWindowPreviewSurface>(),
                (IntPtr)factoryArgs![0]!))
            .AddViewFor<TrackedWindowView, TrackedWindowViewModel>(
                ServiceLifetime.Transient,
                provider => new TrackedWindowView());

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
    }
}
