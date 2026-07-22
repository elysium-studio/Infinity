using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Infinity.Shell.WinUI;

public sealed class SettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddViewFor(ServiceLifetime.Transient,
                provider => new AboutWindow(),
                provider => new AboutViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>()))
            .AddViewFor(ServiceLifetime.Transient,
                provider => new SettingsWindow(),
                provider => new SettingsViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<ISettingViewModel>>()))
            .AddViewFor<DesktopView, ISettingViewModel, DesktopViewModel>(ServiceLifetime.Transient,
                provider => new DesktopView(),
                provider => new DesktopViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<IDesktopViewModel>>()))
            .AddViewFor<PreviewView, ISettingViewModel, PreviewViewModel>(ServiceLifetime.Transient,
                provider => new PreviewView(),
                provider => new PreviewViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<IPreviewViewModel>>()))
            .AddViewFor<WindowsView, ISettingViewModel, WindowsViewModel>(ServiceLifetime.Transient,
                provider => new WindowsView(),
                provider => new WindowsViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<IWindowsViewModel>>()));
    }
}
