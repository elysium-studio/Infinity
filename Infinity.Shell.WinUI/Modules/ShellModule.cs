using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Infinity.Platform.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infinity.Shell.WinUI;

public sealed class ShellModule :
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
            .AddSingleton<IPager>(provider => new Pager(provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IPanState>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWorkspace>(),
                provider.GetRequiredService<IForegroundWindowCoordinator>(),
                provider.GetRequiredService<ILogger<Pager>>()));
    }
}