using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Infinity.Shell.WinUI;

public class TourModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddViewFor(ServiceLifetime.Transient,
                provider => new TourWindow(),
                provider => new TourViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IWritableOptions<Settings>>(),
                    provider.GetRequiredService<IEnumerable<ITourViewModel>>()))
            .AddViewFor<WelcomeView, ITourViewModel, WelcomeViewModel>(ServiceLifetime.Transient,
                provider => new WelcomeView(),
                provider => new WelcomeViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>()))
            .AddViewFor<ScrollTriggerView, ITourViewModel, ScrollTriggerViewModel>(ServiceLifetime.Transient,
                provider => new ScrollTriggerView(),
                provider => new ScrollTriggerViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IKeyLabelProvider>()))
            .AddViewFor<WindowDragTriggerView, ITourViewModel, WindowDragTriggerViewModel>(ServiceLifetime.Transient,
                provider => new WindowDragTriggerView(),
                provider => new WindowDragTriggerViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IKeyLabelProvider>()))
            .AddViewFor<WindowJumpTriggerView, ITourViewModel, WindowJumpTriggerViewModel>(ServiceLifetime.Transient,
                provider => new WindowJumpTriggerView(),
                provider => new WindowJumpTriggerViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IKeyLabelProvider>()))
            .AddViewFor<PageSwitchTriggerView, ITourViewModel, PageSwitchTriggerViewModel>(ServiceLifetime.Transient,
                provider => new PageSwitchTriggerView(),
                provider => new PageSwitchTriggerViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IKeyLabelProvider>()))
            .AddViewFor<WindowNumberTriggerView, ITourViewModel, WindowNumberTriggerViewModel>(ServiceLifetime.Transient,
                provider => new WindowNumberTriggerView(),
                provider => new WindowNumberTriggerViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IKeyLabelProvider>()))
            .AddViewFor<PageNumberSwitchTriggerView, ITourViewModel, PageNumberSwitchTriggerViewModel>(ServiceLifetime.Transient,
                provider => new PageNumberSwitchTriggerView(),
                provider => new PageNumberSwitchTriggerViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Settings>(),
                    provider.GetRequiredService<IKeyLabelProvider>()));
    }
}