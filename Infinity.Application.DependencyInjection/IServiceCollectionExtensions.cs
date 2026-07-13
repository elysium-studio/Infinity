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
            services.AddSingleton<IScrollDeltaAccumulator, ScrollDeltaAccumulator>();
            services.AddSingleton<IPanState, PanState>();

            services.AddSingleton<IWindowStore, WindowStore>();
            services.AddSingleton<IWindowTitleSynchronizer, WindowTitleSynchronizer>();
            services.AddSingleton<IWindowRestoreGuard, WindowRestoreGuard>();

            services.AddSingleton<IWindowTracker>(provider =>
                new WindowTracker(provider.GetRequiredService<IWindowStore>(),
                    provider.GetRequiredService<IWindowGeometryReader>(),
                    provider.GetRequiredService<IWindowFilter>(),
                    provider.GetRequiredService<IWindowAncestorResolver>(),
                    provider.GetRequiredService<IWindowRestoreGuard>(),
                    provider.GetRequiredService<IWindowPlacementRules>(),
                    provider.GetRequiredService<IWindowMoveGuard>(),
                    provider.GetRequiredService<IWindowMover>(),
                    provider.GetRequiredService<IWindowConcealer>(),
                    provider.GetRequiredService<IWindowDragGuard>(),
                    provider.GetRequiredService<ITrackedWindowDragController>(),
                    provider.GetRequiredService<IWindowEnumerator>(),
                    provider.GetRequiredService<IWindowEventListener>(),
                    provider.GetRequiredService<IWorkspace>(),
                    provider.GetRequiredService<IPager>(),
                    provider.GetRequiredService<IPanState>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IMessageWindow>().Handle));

            services.AddSingleton<IScrollInputSource>(provider =>
                new ModifiedScrollInput(provider.GetRequiredService<IPointerInputSource>(),
                    provider.GetRequiredService<IModifierKeyState>()));

            services.AddSingleton<IScroller>(provider =>
            {
                IScrollTimer scrollTimer = provider.GetRequiredService<IScrollTimer>();
                return new Scroller(provider.GetRequiredService<IPanState>(),
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
            services.AddSingleton<IWindowPageCoordinator>(provider => provider.GetRequiredService<WindowPageCoordinator>());
            services.AddSingleton<IWindowNavigationCoordinator>(provider => provider.GetRequiredService<WindowPageCoordinator>());
            services.AddSingleton<IForegroundWindowCoordinator>(provider => provider.GetRequiredService<WindowPageCoordinator>());
            services.AddSingleton<IWindowPageMover>(provider => new WindowPageMover(
                provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IPager>(),
                () => provider.GetRequiredService<IWorkspace>().Width,
                provider.GetRequiredService<ILogger<WindowPageMover>>()));
            services.AddSingleton<IStickyWindowController>(provider => new StickyWindowController(
                provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<ILogger<StickyWindowController>>()));
            services.AddSingleton<ITrackedWindowDragController>(provider => new TrackedWindowDragController(
                provider.GetRequiredService<IWindowStore>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<ILogger<TrackedWindowDragController>>()));
            services.AddSingleton<IThumbnailDragScroller>(provider => new ThumbnailDragScroller(
                provider.GetRequiredService<IModifierKeyState>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IPanState>(),
                provider.GetRequiredService<IDispatcher>(),
                provider.GetRequiredService<Func<WindowDragScrollerConfiguration>>(),
                provider.GetRequiredService<ILogger<ThumbnailDragScroller>>()));
            services.AddSingleton<ISelectionPreviewQueue, SelectionPreviewQueue>();
            services.AddSingleton<IWindowSelector, WindowSelector>();
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
