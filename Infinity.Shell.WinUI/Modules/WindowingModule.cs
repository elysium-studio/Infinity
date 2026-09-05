using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infinity.Shell.WinUI;

public sealed class WindowingModule : IModule
{
    public void Register(IServiceCollection services) => services.AddSingleton<WindowCollection>(provider => new WindowCollection(provider.GetRequiredService<IWindowStore>(), provider.GetRequiredService<IScrollTimer>(), provider.GetRequiredService<IScroller>(), provider.GetRequiredService<IWindowStack>(), provider.GetRequiredService<IForegroundWindowTracker>(), provider.GetRequiredService<IWindowEventListener>(), provider.GetRequiredService<IWorkspace>(), provider.GetRequiredService<IForegroundWindowCoordinator>(), provider.GetRequiredService<IWindowNavigationCoordinator>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<ILogger<WindowCollection>>())).AddSingleton<IWindowCollection>(provider => provider.GetRequiredService<WindowCollection>());
}
