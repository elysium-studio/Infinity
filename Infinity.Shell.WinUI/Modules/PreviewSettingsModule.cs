using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Infinity.Shell.WinUI;

public sealed class PreviewSettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddViewFor<PreviewSizeView, IPreviewViewModel, PreviewSizeViewModel>(ServiceLifetime.Transient,
                provider => new PreviewSizeView(),
                provider => new PreviewSizeViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => (int)config.PreviewSize,
                    (config, previewSize) => config.PreviewSize = (PreviewSize)previewSize))
            .AddViewFor<PreviewBackgroundView, IPreviewViewModel, PreviewBackgroundViewModel>(ServiceLifetime.Transient,
                provider => new PreviewBackgroundView(),
                provider => new PreviewBackgroundViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => config.ShowDesktopBackground,
                    (config, showDesktopBackground) => config.ShowDesktopBackground = showDesktopBackground))
            .AddViewFor<PreviewPositionView, IPreviewViewModel, PreviewPositionViewModel>(ServiceLifetime.Transient,
                provider => new PreviewPositionView(),
                provider => new PreviewPositionViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => (int)config.PreviewPosition,
                    (config, previewPosition) => config.PreviewPosition = (PreviewPosition)previewPosition));
    }
}
