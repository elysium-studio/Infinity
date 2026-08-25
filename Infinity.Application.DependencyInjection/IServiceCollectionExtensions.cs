using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infinity.Application.DependencyInjection;

public static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfinityApplication()
        {
            services.AddSingleton<IPanState, PanState>();
            services.AddSingleton<IScrollPresentationSession, ScrollPresentationSession>();

            services.AddSingleton<IWindowStore, WindowStore>();
            services.AddSingleton<IWindowTitleSynchronizer, WindowTitleSynchronizer>();
            services.AddSingleton<IWindowRestoreGuard, WindowRestoreGuard>();
            services.AddSingleton<StartupPageRestorer>();

            services.AddSingleton<IWindowTracker>(provider =>
                new WindowTracker(provider.GetRequiredService<IWindowStore>(),
                    provider.GetRequiredService<IWindowGeometryReader>(),
                    provider.GetRequiredService<IWindowFilter>(),
                    provider.GetRequiredService<IWindowAncestorResolver>(),
                    provider.GetRequiredService<IWindowRestoreGuard>(),
                    provider.GetRequiredService<IWindowMoveGuard>(),
                    provider.GetRequiredService<IWindowConcealer>(),
                    provider.GetRequiredService<IWindowDragGuard>(),
                    provider.GetRequiredService<ITrackedWindowDragController>(),
                    provider.GetRequiredService<IWindowEnumerator>(),
                    provider.GetRequiredService<IWindowEventListener>(),
                    provider.GetRequiredService<IPanState>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<ILogger<WindowTracker>>(),
                    provider.GetRequiredService<IMessageWindow>().Handle));

            services.AddSingleton<IScrollInputSource>(provider =>
                new ModifiedScrollInput(provider.GetRequiredService<IPointerInputSource>(),
                    provider.GetRequiredService<IModifierKeyState>()));

            services.AddSingleton<IScroller>(provider =>
            {
                IScrollTimer scrollTimer = provider.GetRequiredService<IScrollTimer>();
                return new Scroller(provider.GetRequiredService<IPanState>(),
                    provider.GetRequiredService<IScrollPresentationSession>(),
                    provider.GetRequiredService<IWindowStore>(),
                    provider.GetRequiredService<IWindowMover>(),
                    provider.GetRequiredService<IWindowConcealer>(),
                    provider.GetRequiredService<IWindowMoveGuard>(),
                    provider.GetRequiredService<IWindowDragGuard>(),
                    provider.GetRequiredService<IScrollInputSource>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<Func<ScrollerConfiguration>>(),
                    new PixelScrollMotion(),
                    new EasingScrollMotion(),
                    new MomentumScrollMotion(),
                    scrollTimer.Start,
                    scrollTimer.Stop,
                    provider.GetRequiredService<ILogger<Scroller>>());
            });

            services.AddSingleton<WindowPageCoordinator>();
            services.AddSingleton<IWindowNavigationCoordinator>(provider => provider.GetRequiredService<WindowPageCoordinator>());
            services.AddSingleton<IForegroundWindowCoordinator>(provider => provider.GetRequiredService<WindowPageCoordinator>());
            services.AddSingleton<ITrackedForegroundWindowSource>(provider => provider.GetRequiredService<WindowPageCoordinator>());
            services.AddSingleton<ITrackedWindowDragController>(provider => new TrackedWindowDragController(provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<ILogger<TrackedWindowDragController>>()));
            services.AddSingleton<IWindowDragScroller, WindowDragScroller>();

            services.AddSingleton<WindowArrowSwitchGesture>();
            services.AddSingleton<WindowArrowMoveGesture>();
            services.AddSingleton<WindowNumberSwitchGesture>();
            services.AddSingleton<WindowNumberMoveGesture>();
            services.AddSingleton<IPageGesture>(provider => provider.GetRequiredService<WindowArrowSwitchGesture>());
            services.AddSingleton<IPageGesture>(provider => provider.GetRequiredService<WindowArrowMoveGesture>());
            services.AddSingleton<IPageGesture>(provider => provider.GetRequiredService<WindowNumberSwitchGesture>());
            services.AddSingleton<IPageGesture>(provider => provider.GetRequiredService<WindowNumberMoveGesture>());
            services.AddSingleton<IPageGestureSource, PageGestureSource>();
            services.AddSingleton<IWindowPageJumper, WindowPageJumper>();

            services.AddHostedService<PagerLifetime>();

            return services;
        }
    }
}
