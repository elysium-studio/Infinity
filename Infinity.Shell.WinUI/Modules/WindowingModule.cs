using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ApplicationServices = Infinity.Application;

namespace Infinity.Shell.WinUI;

public sealed class WindowingModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddSingleton<ApplicationServices.WindowCollection>(provider => new ApplicationServices.WindowCollection(provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IScrollTimer>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWindowStack>(),
                provider.GetRequiredService<IForegroundWindowTracker>(),
                provider.GetRequiredService<global::Elysium.Platform.Abstractions.IWindowEventListener>(),
                provider.GetRequiredService<global::Elysium.Platform.Abstractions.IWorkspace>(),
                provider.GetRequiredService<IForegroundWindowCoordinator>(),
                provider.GetRequiredService<IWindowNavigationCoordinator>(),
                provider.GetRequiredService<IDispatcher>(),
                provider.GetRequiredService<ILogger<ApplicationServices.WindowCollection>>()))
            .AddSingleton<IWindowCollection>(provider => provider.GetRequiredService<ApplicationServices.WindowCollection>());
    }
}
