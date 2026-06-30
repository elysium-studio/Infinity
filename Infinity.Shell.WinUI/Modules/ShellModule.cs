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
                    ? keys : [[VirtualKeys.VK_LWIN, VirtualKeys.VK_RWIN], [VirtualKeys.VK_LCONTROL, VirtualKeys.VK_RCONTROL]]))
            .AddSingleton<IShellLayoutCalculator, ShellLayoutCalculator>()
            .AddSingleton<IScrollTimer, DwmFlushScrollTimer>()
            .AddSingleton<IPager>(provider => new Pager(provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IPanState>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWorkspace>(),
                provider.GetRequiredService<IWindowTracker>(),
                provider.GetRequiredService<ILogger<Pager>>()))
            .AddSingleton<ITrackedWindowFilter, TrackedWindowFilter>()
            .AddSingleton<IWindowFilterEffects>(provider => new WindowFadeFilterEffects(provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IWindowOpacity>(),
                provider.GetRequiredService<ITrackedWindowFilter>(),
                () => provider.GetRequiredService<IOptionsMonitor<Settings>>().CurrentValue.HideFilteredWindows))
            .AddSingleton<ITrackedWindowCollection, TrackedWindowCollection>()
            .AddSingleton<IWindowPageCoordinator>(provider => new WindowPageCoordinator(provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IPager>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWorkspace>(),
                provider.GetRequiredService<IWindowActivator>(),
                provider.GetRequiredService<IDispatcher>()))
            .AddSingleton<IWindowFilterState>(provider => new WindowFilterState(provider.GetRequiredService<ITrackedWindowFilter>()))
            .AddSingleton<IWindowFilterEffectController>(provider => new WindowFilterEffectController(provider.GetServices<IWindowFilterEffects>()))
            .AddSingleton<IDesktopBackgroundController>(provider => new DesktopBackgroundController(
                provider.GetRequiredService<IDesktopBackgroundSource>(),
                provider.GetRequiredService<IDispatcher>()))
            .AddSingleton<IWindowSelector>(provider => new WindowSelector(new SelectionPreviewQueue(provider.GetRequiredService<IWindowZOrder>()),
                provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IPager>(),
                provider.GetRequiredService<IWorkspace>()))
            .AddSingleton<IWindowCollection>(provider => new WindowCollection(provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IScrollTimer>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWindowZOrder>(),
                provider.GetRequiredService<IWorkspace>(),
                provider.GetRequiredService<IWindowFilterState>(),
                provider.GetRequiredService<IWindowPageCoordinator>(),
                provider.GetRequiredService<ITrackedWindowCollection>(),
                provider.GetRequiredService<IDispatcher>(),
                provider.GetRequiredService<ILogger<WindowCollection>>()))
            .RegisterFactory((provider, factoryArgs) => new TrackedWindowViewModel(provider,
                provider.GetRequiredService<IServiceFactory>(),
                provider.GetRequiredService<IMessenger>(),
                provider.GetRequiredService<IDisposer>(),
                provider.GetRequiredService<IWindowController>(),
                provider.GetRequiredService<IWindowPreviewSurface>(),
                (IntPtr)factoryArgs![0]!,
                (Action<IntPtr>)factoryArgs[1]!))
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
                    provider.GetRequiredService<IPanState>()))
            .AddViewFor(ServiceLifetime.Singleton,
                provider => new DesktopFlyoutView(provider.GetRequiredService<IWindowPreviewSurface>()),
                provider => new DesktopFlyoutViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IScroller>(),
                    provider.GetRequiredService<IWorkspace>(),
                    provider.GetRequiredService<IPointerInputSource>(),
                    provider.GetRequiredService<IModifierKeyState>(),
                    provider.GetRequiredService<IWindowDragScroller>(),
                    provider.GetRequiredService<Settings>()))
            .AddView(ServiceLifetime.Singleton,
                provider => new ScrollTriggerView())
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
                    provider.GetRequiredService<IWindowDragScroller>(),
                    provider.GetRequiredService<IWindowCollection>(),
                    provider.GetRequiredService<ITrackedWindowCollection>(),
                    provider.GetRequiredService<IWindowSelector>(),
                    provider.GetRequiredService<IWindowFilterState>(),
                    provider.GetRequiredService<IWindowFilterEffectController>(),
                    provider.GetRequiredService<IDesktopBackgroundController>(),
                    provider.GetRequiredService<IWindowPageCoordinator>(),
                    provider.GetRequiredService<INavigator>(),
                    provider.GetRequiredService<IOptionsMonitor<Settings>>(),
                    provider.GetRequiredService<IApplicationLifetime>(),
                    provider.GetRequiredService<ILogger<TrackedWindowCollectionViewModel>>()))
            .AddViewFor<TrackedWindowView, TrackedWindowViewModel>(ServiceLifetime.Transient,
                provider => new TrackedWindowView());
    }
}