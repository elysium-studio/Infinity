using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using ApplicationServices = Infinity.Application;

namespace Infinity.Shell.WinUI;

public sealed class WindowingModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddSingleton<IWindowFilterState, TrackedWindowFilter>()
            .AddSingleton<IWindowPlacementRules, WindowPlacementRules>()
            .AddSingleton<IWindowPeekSource, ApplicationServices.WindowPeekSource>()
            .AddSingleton<IPeekSource>(provider => provider.GetRequiredService<IWindowPeekSource>())
            .AddSingleton<IPeekSource>(provider => new ApplicationServices.FilterPeekSource(provider.GetRequiredService<IWindowFilterState>()))
            .AddSingleton<IWindowPeekController>(provider => new ApplicationServices.WindowPeekController(provider.GetRequiredService<IWindowStore>(),
                provider.GetServices<IPeekSource>(),
                provider.GetRequiredService<IWindowConcealer>(),
                provider.GetRequiredService<IScroller>(),
                () => provider.GetRequiredService<IOptionsMonitor<Settings>>().CurrentValue.HideFilteredWindows))
            .AddSingleton<ITrackedWindowCollection, TrackedWindowCollection>()
            .AddSingleton<ApplicationServices.WindowCollection>(provider => new ApplicationServices.WindowCollection(provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IScrollTimer>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWindowStack>(),
                provider.GetRequiredService<IForegroundWindowTracker>(),
                provider.GetRequiredService<global::Elysium.Platform.Abstractions.IWindowEventListener>(),
                provider.GetRequiredService<global::Elysium.Platform.Abstractions.IWorkspace>(),
                provider.GetRequiredService<IWindowFilterState>(),
                provider.GetRequiredService<IForegroundWindowCoordinator>(),
                provider.GetRequiredService<IWindowNavigationCoordinator>(),
                provider.GetRequiredService<IDispatcher>(),
                provider.GetRequiredService<ILogger<ApplicationServices.WindowCollection>>()))
            .AddSingleton<IWindowCollection>(provider => provider.GetRequiredService<ApplicationServices.WindowCollection>())
            .AddSingleton<IWindowCollectionLifetime>(provider => provider.GetRequiredService<ApplicationServices.WindowCollection>())
            .RegisterFactory((provider, factoryArgs) => new TrackedWindowViewModel(provider,
                provider.GetRequiredService<IServiceFactory>(),
                provider.GetRequiredService<IMessenger>(),
                provider.GetRequiredService<IDisposer>(),
                provider.GetRequiredService<IWindowController>(),
                provider.GetRequiredService<IWindowPageMover>(),
                provider.GetRequiredService<IWindowPlacementRules>(),
                provider.GetRequiredService<IStickyWindowController>(),
                provider.GetRequiredService<ITrackedWindowDragController>(),
                provider.GetRequiredService<IPager>(),
                provider.GetRequiredService<IOptionsMonitor<Settings>>(),
                provider.GetRequiredService<ITextLocalizer>(),
                provider.GetRequiredService<ILogger<TrackedWindowViewModel>>(),
                (IntPtr)factoryArgs![0]!))
            .AddViewFor<TrackedWindowView, TrackedWindowViewModel>(ServiceLifetime.Transient,
                provider => new TrackedWindowView(provider.GetRequiredService<IStringLocalizer>(),
                    provider.GetRequiredService<IThumbnailDragScroller>(),
                    provider.GetRequiredService<IWindowNavigationCoordinator>(),
                    provider.GetRequiredService<IWindowPreviewSurface>(),
                    provider.GetRequiredService<ILogger<TrackedWindowView>>()));

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
