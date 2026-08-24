using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            .AddSingleton<IWindowCollectionLifetime>(provider => provider.GetRequiredService<ApplicationServices.WindowCollection>());
    }
}
