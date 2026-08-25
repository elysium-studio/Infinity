using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infinity.Shell.WinUI;

public sealed class DesktopSettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddViewFor<VirtualPagesView, IDesktopViewModel, VirtualPagesViewModel>(ServiceLifetime.Transient,
                provider => new VirtualPagesView(),
                provider => new VirtualPagesViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => (int)config.VirtualPagesMode,
                    (config, virtualPagesMode) => config.VirtualPagesMode = (VirtualPagesMode)virtualPagesMode))
            .AddViewFor<VirtualPagesCountView, IDesktopViewModel, VirtualPagesCountViewModel>(ServiceLifetime.Transient,
                provider => new VirtualPagesCountView(),
                provider => new VirtualPagesCountViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => config.VirtualPagesCount,
                    (config, virtualPagesCount) => config.VirtualPagesCount = virtualPagesCount))
            .AddViewFor<ScrollSpeedView, IDesktopViewModel, ScrollSpeedViewModel>(ServiceLifetime.Transient,
                provider => new ScrollSpeedView(),
                provider => new ScrollSpeedViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => (int)config.ScrollSpeed,
                    (config, scrollSpeed) => config.ScrollSpeed = (ScrollSpeed)scrollSpeed))
            .AddViewFor<DragScrollSpeedView, IDesktopViewModel, DragScrollSpeedViewModel>(ServiceLifetime.Transient,
                provider => new DragScrollSpeedView(),
                provider => new DragScrollSpeedViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => (int)config.DragScrollSpeed,
                    (config, dragScrollSpeed) => config.DragScrollSpeed = (DragScrollSpeed)dragScrollSpeed))
            .AddViewFor<ScrollModifierKeyView, IDesktopViewModel, ScrollModifierKeyViewModel>(ServiceLifetime.Transient,
                provider => new ScrollModifierKeyView(),
                provider => new ScrollModifierKeyViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => config.ScrollModifierKeys,
                    (config, scrollModifierKeys) => config.ScrollModifierKeys = scrollModifierKeys!,
                    provider.GetRequiredService<IHotKeysBuilder>(),
                    provider.GetRequiredService<HotKeysBuilderOptions>(),
                    provider.GetRequiredService<IKeyLabelProvider>(),
                    provider.GetRequiredService<ITextLocalizer>()));
    }
}
