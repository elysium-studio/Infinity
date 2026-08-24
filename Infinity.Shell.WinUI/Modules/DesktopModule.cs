using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Elysium.Presentation.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infinity.Shell.WinUI;

public sealed class DesktopModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddSingleton<DesktopScrollPreviewAnimator>()
            .AddSingleton<PageTitleStore>()
            .AddSingleton<PageNavigationPublisher>()
            .AddSingleton(provider => new DesktopScrollPreviewView(provider.GetRequiredService<IWindowPreviewSurface>(),
                provider.GetRequiredService<IWindowCollection>(),
                provider.GetRequiredService<IShellLayoutCalculator>(),
                provider.GetRequiredService<IScroller>(),
                provider.GetRequiredService<IWorkspace>(),
                provider.GetRequiredService<ITaskbarLocator>(),
                provider.GetRequiredService<IWindowGeometryReader>(),
                provider.GetRequiredService<DesktopScrollPreviewAnimator>(),
                provider.GetRequiredService<ILogger<DesktopScrollPreviewView>>()))
            .AddViewFor(ServiceLifetime.Singleton,
                provider => new PageTintView(provider.GetRequiredService<DesktopScrollPreviewView>()),
                provider => new PageTintViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IPointerInputSource>(),
                    provider.GetRequiredService<IModifierKeyState>(),
                    provider.GetRequiredService<IWindowDragScroller>(),
                    provider.GetRequiredService<IPageGestureSource>(),
                    provider.GetRequiredService<IOptionsMonitor<Settings>>(),
                    provider.GetRequiredService<IScroller>(),
                    provider.GetRequiredService<IScrollPresentationSession>(),
                    provider.GetRequiredService<IWindowPreviewSurface>(),
                    provider.GetRequiredService<IInfinityGlanceBridge>()))
            .AddView(ServiceLifetime.Singleton, provider => new ScrollTriggerView());

        services.Subscribe<PageNavigationPublisher>((provider, publisher) =>
        {
            publisher.Start();
            return publisher.Stop;
        });
    }
}
