using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Elysium.Presentation.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

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
            .AddSingleton<DesktopDragBoundaryCalculator>()
            .AddSingleton<DesktopPageReorderController>()
            .AddSingleton<DesktopBackgroundBrushFactory>()
            .AddSingleton<DesktopPageStrip>()
            .AddSingleton<DesktopOverviewDragScroller>()
            .AddSingleton<DesktopDragCursorConfinement>()
            .AddSingleton<DesktopWindowDragPositionResolver>()
            .AddSingleton<DesktopWindowPreviewFactory>()
            .AddSingleton<DesktopWindowPreviewCollection>()
            .AddSingleton<WindowInputTransparencyController>()
            .AddSingleton<PageTitleStore>()
            .AddSingleton<PageNavigationPublisher>()
            .AddSingleton(provider => CreateDesktopScrollPreviewView(provider))
            .AddViewFor(ServiceLifetime.Singleton, provider => CreateDesktopOverviewView(provider), provider => CreateDesktopOverviewViewModel(provider))
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

    private static DesktopScrollPreviewView CreateDesktopScrollPreviewView(IServiceProvider provider)
    {
        IWindowPreviewSurface windowPreviewSurface = provider.GetRequiredService<IWindowPreviewSurface>();
        IWindowCollection windowCollection = provider.GetRequiredService<IWindowCollection>();
        IShellLayoutCalculator layoutCalculator = provider.GetRequiredService<IShellLayoutCalculator>();
        IPanState panState = provider.GetRequiredService<IPanState>();
        IScroller scroller = provider.GetRequiredService<IScroller>();
        IPager pager = provider.GetRequiredService<IPager>();
        IWorkspace workspace = provider.GetRequiredService<IWorkspace>();
        ITaskbarLocator taskbarLocator = provider.GetRequiredService<ITaskbarLocator>();
        DesktopPageLayoutCalculator pageLayoutCalculator = provider.GetRequiredService<DesktopPageLayoutCalculator>();
        DesktopScrollPreviewAnimator animator = provider.GetRequiredService<DesktopScrollPreviewAnimator>();
        DesktopPageStrip pageStrip = provider.GetRequiredService<DesktopPageStrip>();
        DesktopWindowPreviewCollection previews = provider.GetRequiredService<DesktopWindowPreviewCollection>();
        DesktopDragCursorConfinement cursorConfinement = provider.GetRequiredService<DesktopDragCursorConfinement>();

        return new DesktopScrollPreviewView(windowPreviewSurface, windowCollection, layoutCalculator, panState, scroller, pager, workspace, taskbarLocator, pageLayoutCalculator, animator, pageStrip, previews, cursorConfinement);
    }

    private static DesktopOverviewView CreateDesktopOverviewView(IServiceProvider provider)
    {
        DesktopScrollPreviewView desktopScrollPreview = provider.GetRequiredService<DesktopScrollPreviewView>();
        DesktopOverviewBackdropAnimator backdropAnimator = provider.GetRequiredService<DesktopOverviewBackdropAnimator>();
        DesktopOverviewWallpaperPresenter wallpaperPresenter = provider.GetRequiredService<DesktopOverviewWallpaperPresenter>();
        WindowInputTransparencyController inputController = provider.GetRequiredService<WindowInputTransparencyController>();
        IDesktopBackgroundSource backgroundSource = provider.GetRequiredService<IDesktopBackgroundSource>();
        IKeyboardInputSource keyboardInputSource = provider.GetRequiredService<IKeyboardInputSource>();

        return new DesktopOverviewView(desktopScrollPreview, backdropAnimator, wallpaperPresenter, inputController, backgroundSource, keyboardInputSource);
    }

    private static DesktopOverviewViewModel CreateDesktopOverviewViewModel(IServiceProvider provider)
    {
        IServiceFactory serviceFactory = provider.GetRequiredService<IServiceFactory>();
        IMessenger messenger = provider.GetRequiredService<IMessenger>();
        IDisposer disposer = provider.GetRequiredService<IDisposer>();
        IDispatcher dispatcher = provider.GetRequiredService<IDispatcher>();
        IPointerInputSource pointerInputSource = provider.GetRequiredService<IPointerInputSource>();
        IModifierKeyState modifierKeyState = provider.GetRequiredService<IModifierKeyState>();
        IWindowDragScroller windowDragScroller = provider.GetRequiredService<IWindowDragScroller>();
        IPageGestureSource pageGestureSource = provider.GetRequiredService<IPageGestureSource>();
        IPager pager = provider.GetRequiredService<IPager>();
        IScroller scroller = provider.GetRequiredService<IScroller>();
        IScrollPresentationSession scrollPresentationSession = provider.GetRequiredService<IScrollPresentationSession>();
        IWindowPreviewSurface windowPreviewSurface = provider.GetRequiredService<IWindowPreviewSurface>();
        IWindowNavigationCoordinator windowNavigationCoordinator = provider.GetRequiredService<IWindowNavigationCoordinator>();
        IInfinityGlanceBridge infinityGlanceBridge = provider.GetRequiredService<IInfinityGlanceBridge>();
        INavigator navigator = provider.GetRequiredService<INavigator>();
        ILogger<DesktopOverviewViewModel> logger = provider.GetRequiredService<ILogger<DesktopOverviewViewModel>>();

        return new DesktopOverviewViewModel(provider, serviceFactory, messenger, disposer, dispatcher, pointerInputSource, modifierKeyState, windowDragScroller, pageGestureSource, pager, scroller, scrollPresentationSession, windowPreviewSurface, windowNavigationCoordinator, infinityGlanceBridge, navigator, logger);
    }
}
