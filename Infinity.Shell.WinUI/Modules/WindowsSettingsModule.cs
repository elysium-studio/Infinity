using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Infinity.Shell.WinUI;

public sealed class WindowsSettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services.AddViewFor<StartWithWindowsView, IWindowsViewModel, StartWithWindowsViewModel>(ServiceLifetime.Transient,
            provider => new StartWithWindowsView(),
            provider => new StartWithWindowsViewModel(provider,
                provider.GetRequiredService<IServiceFactory>(),
                provider.GetRequiredService<IMessenger>(),
                provider.GetRequiredService<IDisposer>(),
                provider.GetRequiredService<IDispatcher>(),
                provider.GetRequiredService<Settings>(),
                provider.GetRequiredService<IWritableOptions<Settings>>(),
                config => config.StartWithWindows,
                (config, startWithWindows) => config.StartWithWindows = startWithWindows));
    }
}