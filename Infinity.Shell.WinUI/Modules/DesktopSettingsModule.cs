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

public sealed class DesktopSettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddTransient<ScrollModifierKeyRecorder>()
            .AddViewFor<PagesView, IDesktopViewModel, PagesViewModel>(ServiceLifetime.Transient, _ => new PagesView(), CreatePagesViewModel)
            .AddViewFor<VirtualPagesView, IPagesViewModel, VirtualPagesViewModel>(ServiceLifetime.Transient,
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
            .AddViewFor<VirtualPagesCountView, IPagesViewModel, VirtualPagesCountViewModel>(ServiceLifetime.Transient,
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
            .AddViewFor<ScrollingView, IDesktopViewModel, ScrollingViewModel>(ServiceLifetime.Transient, _ => new ScrollingView(), CreateScrollingViewModel)
            .AddViewFor<ScrollModifierKeyView, IScrollingViewModel, ScrollModifierKeyViewModel>(ServiceLifetime.Transient,
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
                    provider.GetRequiredService<ScrollModifierKeyRecorder>()))
            .AddViewFor<ScrollSpeedView, IScrollingViewModel, ScrollSpeedViewModel>(ServiceLifetime.Transient,
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
            .AddViewFor<DragScrollSpeedView, IScrollingViewModel, DragScrollSpeedViewModel>(ServiceLifetime.Transient,
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
            .AddViewFor<OverviewEdgeScrollingView, IScrollingViewModel, OverviewEdgeScrollingViewModel>(ServiceLifetime.Transient, _ => new OverviewEdgeScrollingView(), CreateOverviewEdgeScrollingViewModel)
            .AddViewFor<OverviewView, IDesktopViewModel, OverviewViewModel>(ServiceLifetime.Transient, _ => new OverviewView(), CreateOverviewViewModel)
            .AddViewFor<SnapAssistanceView, IOverviewViewModel, SnapAssistanceViewModel>(ServiceLifetime.Transient, _ => new SnapAssistanceView(), CreateSnapAssistanceViewModel)
            .AddViewFor<SpanCompatibleDisplaysView, IOverviewViewModel, SpanCompatibleDisplaysViewModel>(ServiceLifetime.Transient, _ => new SpanCompatibleDisplaysView(), CreateSpanCompatibleDisplaysViewModel)
            .AddViewFor<AdvancedView, IDesktopViewModel, AdvancedViewModel>(ServiceLifetime.Transient, _ => new AdvancedView(), CreateAdvancedViewModel)
            .AddViewFor<ResetPageCustomizationsView, IAdvancedViewModel, ResetPageCustomizationsViewModel>(ServiceLifetime.Transient, _ => new ResetPageCustomizationsView(), CreateResetPageCustomizationsViewModel);
    }

    private static PagesViewModel CreatePagesViewModel(IServiceProvider provider) => new(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<ITextLocalizer>(), provider.GetRequiredService<IEnumerable<IPagesViewModel>>());

    private static ScrollingViewModel CreateScrollingViewModel(IServiceProvider provider) => new(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<ITextLocalizer>(), provider.GetRequiredService<IEnumerable<IScrollingViewModel>>());

    private static OverviewViewModel CreateOverviewViewModel(IServiceProvider provider) => new(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<ITextLocalizer>(), provider.GetRequiredService<IEnumerable<IOverviewViewModel>>());

    private static AdvancedViewModel CreateAdvancedViewModel(IServiceProvider provider) => new(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<ITextLocalizer>(), provider.GetRequiredService<IEnumerable<IAdvancedViewModel>>());

    private static SpanCompatibleDisplaysViewModel CreateSpanCompatibleDisplaysViewModel(IServiceProvider provider) => new(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<Settings>(), provider.GetRequiredService<IWritableOptions<Settings>>());

    private static OverviewEdgeScrollingViewModel CreateOverviewEdgeScrollingViewModel(IServiceProvider provider) => new(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<Settings>(), provider.GetRequiredService<IWritableOptions<Settings>>());

    private static SnapAssistanceViewModel CreateSnapAssistanceViewModel(IServiceProvider provider) => new(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<Settings>(), provider.GetRequiredService<IWritableOptions<Settings>>());

    private static ResetPageCustomizationsViewModel CreateResetPageCustomizationsViewModel(IServiceProvider provider) => new(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<Settings>(), provider.GetRequiredService<IWritableOptions<Settings>>(), provider.GetRequiredService<ITextLocalizer>());
}
