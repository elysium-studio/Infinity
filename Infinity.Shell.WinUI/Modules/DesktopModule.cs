using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Infinity.Shell.WinUI;

public sealed class DesktopModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddSingleton<DesktopOverviewBackdropAnimator>()
            .AddSingleton<DesktopOverviewWallpaperPresenter>()
            .AddSingleton<DesktopScrollPreviewAnimator>()
            .AddSingleton<DesktopPageLayoutCalculator>()
            .AddSingleton<DesktopBackgroundBrushFactory>()
            .AddSingleton<DesktopPageStrip>()
            .AddSingleton<DesktopWindowPreviewFactory>()
            .AddSingleton<DesktopWindowPreviewCollection>()
            .AddSingleton<WindowInputTransparencyController>()
            .AddSingleton<PageTitleStore>()
            .AddSingleton<PageNavigationPublisher>()
            .AddSingleton(provider => new DesktopScrollPreviewView(provider.GetRequiredService<IWindowPreviewSurface>(),
                provider.GetRequiredService<IWindowCollection>(),
                provider.GetRequiredService<IShellLayoutCalculator>(),
                provider.GetRequiredService<IPanState>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWorkspace>(),
                provider.GetRequiredService<ITaskbarLocator>(),
                provider.GetRequiredService<DesktopPageLayoutCalculator>(),
                provider.GetRequiredService<DesktopScrollPreviewAnimator>(),
                provider.GetRequiredService<DesktopPageStrip>(),
                provider.GetRequiredService<DesktopWindowPreviewCollection>()))
            .AddViewFor(ServiceLifetime.Singleton,
                provider => new DesktopOverviewView(provider.GetRequiredService<DesktopScrollPreviewView>(),
                    provider.GetRequiredService<DesktopOverviewBackdropAnimator>(),
                    provider.GetRequiredService<DesktopOverviewWallpaperPresenter>(),
                    provider.GetRequiredService<WindowInputTransparencyController>(),
                    provider.GetRequiredService<IDesktopBackgroundSource>()),
                provider => new DesktopOverviewViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IPointerInputSource>(),
                    provider.GetRequiredService<IModifierKeyState>(),
                    provider.GetRequiredService<IWindowDragScroller>(),
                    provider.GetRequiredService<IPageGestureSource>(),
                    provider.GetRequiredService<IPager>(),
                    provider.GetRequiredService<IScroller>(),
                    provider.GetRequiredService<IScrollPresentationSession>(),
                    provider.GetRequiredService<IWindowPreviewSurface>(),
                    provider.GetRequiredService<IWindowNavigationCoordinator>(),
                    provider.GetRequiredService<IInfinityGlanceBridge>()))
            .AddView(ServiceLifetime.Singleton, provider => new ScrollTriggerView());

        services.Subscribe<PageNavigationPublisher>((provider, publisher) =>
        {
            publisher.Start();
            return publisher.Stop;
        });

        services.Subscribe<IDesktopBackgroundSource>((provider, backgroundSource) =>
        {
            backgroundSource.Start();
            return backgroundSource.Stop;
        });
    }
}
