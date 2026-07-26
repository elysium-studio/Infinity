using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Elysium.Presentation.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using IApplicationLifetime = Elysium.Application.Abstractions.IApplicationLifetime;

namespace Infinity.Shell.WinUI;

public sealed class DesktopModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddSingleton<IDesktopBackgroundController>(provider => new DesktopBackgroundController(provider.GetRequiredService<IDesktopBackgroundSource>(),
                provider.GetRequiredService<IDispatcher>()))
            .AddViewFor(ServiceLifetime.Singleton,
                provider => new PageTintView(provider.GetRequiredService<IMonitorLocator>(),
                    provider.GetRequiredService<ITaskbarLocator>()),
                provider => new PageTintViewModel(provider,
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
                    provider.GetRequiredService<IPanState>(),
                    provider.GetRequiredService<IInfinityGlanceBridge>(),
                    provider.GetRequiredService<ITextLocalizer>(),
                    provider.GetRequiredService<ILogger<PageTintViewModel>>()))
            .AddViewFor(ServiceLifetime.Singleton,
                provider => new DesktopFlyoutView(provider.GetRequiredService<IWindowPreviewSurface>()),
                provider => new DesktopFlyoutViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IWorkspace>(),
                    provider.GetRequiredService<IModifierKeyState>(),
                    provider.GetRequiredService<IInfinityGlanceBridge>(),
                    provider.GetRequiredService<Settings>()))
            .AddView(ServiceLifetime.Singleton, provider => new ScrollTriggerView())
            .AddViewFor(ServiceLifetime.Singleton,
                provider => new TrackedWindowCollectionView(),
                provider => new TrackedWindowCollectionViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IWorkspace>(),
                    provider.GetRequiredService<IShellLayoutCalculator>(),
                    provider.GetRequiredService<IPager>(),
                    provider.GetRequiredService<IPanState>(),
                    provider.GetRequiredService<IScroller>(),
                    provider.GetRequiredService<ITrackedWindowDragController>(),
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
                    provider.GetRequiredService<ILogger<TrackedWindowCollectionViewModel>>()));

        services.Subscribe<IDesktopBackgroundController>((provider, backgroundController) =>
        {
            IMessenger messenger = provider.GetRequiredService<IMessenger>();

            void HandleBackgroundChanged(object? sender, EventArgs args) =>
                messenger.Send(new DesktopBackgroundChangedEventArgs());

            backgroundController.BackgroundChanged += HandleBackgroundChanged;

            return () => backgroundController.BackgroundChanged -= HandleBackgroundChanged;
        });
    }
}
