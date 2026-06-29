using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Infinity.Shell.WinUI;

public class SettingsModule :
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
            .AddViewFor<WindowsView, ISettingViewModel, WindowsViewModel>(ServiceLifetime.Transient,
                provider => new WindowsView(),
                provider => new WindowsViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<IWindowsViewModel>>()))
            .AddViewFor<StartWithWindowsView, IWindowsViewModel, StartWithWindowsViewModel>(ServiceLifetime.Transient,
                provider => new StartWithWindowsView(),
                provider => new StartWithWindowsViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => config.StartWithWindows,
                    (config, startWithWindows) => config.StartWithWindows = startWithWindows))
            .AddViewFor<DesktopView, ISettingViewModel, DesktopViewModel>(ServiceLifetime.Transient,
                provider => new DesktopView(),
                provider => new DesktopViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<IDesktopViewModel>>()))
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
                    provider.GetRequiredService<IKeyLabelProvider>()))
            .AddViewFor<FadeFilteredWindowsView, IDesktopViewModel, FadeFilteredWindowsViewModel>(ServiceLifetime.Transient,
                provider => new FadeFilteredWindowsView(),
                provider => new FadeFilteredWindowsViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => config.FadeFilteredWindows,
                    (config, fadeFilteredWindows) => config.FadeFilteredWindows = fadeFilteredWindows))
            .AddViewFor<DesktopBlurView, IDesktopViewModel, DesktopBlurViewModel>(ServiceLifetime.Transient,
                provider => new DesktopBlurView(),
                provider => new DesktopBlurViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    config => config.DesktopBlur,
                    (config, desktopBlur) => config.DesktopBlur = desktopBlur))
            .AddViewFor<PreviewView, ISettingViewModel, PreviewViewModel>(ServiceLifetime.Transient,
                provider => new PreviewView(),
                provider => new PreviewViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<IPreviewViewModel>>()))
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