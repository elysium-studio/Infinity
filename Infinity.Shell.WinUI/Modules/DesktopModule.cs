using System;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infinity.Shell.WinUI;

public sealed class DesktopModule : IModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<WindowCaptureAccess>().AddSingleton<WindowCapturePreviewSurface>().AddSingleton<IWindowPreviewSurface>(provider => provider.GetRequiredService<WindowCapturePreviewSurface>()).AddSingleton<DesktopOverviewBackdropAnimator>().AddSingleton<DesktopWallpaperSurfaceProvider>().AddSingleton<DesktopWallpaperPreloader>().AddSingleton<DesktopWallpaperBrushFactory>().AddSingleton<DesktopWallpaperColorSampler>().AddSingleton<DesktopOverviewForegroundThemeResolver>().AddSingleton<DesktopOverviewWallpaperPresenter>().AddSingleton<DesktopScrollPreviewAnimator>().AddSingleton<DesktopOverviewChromeAnimator>().AddSingleton<DesktopOverviewClockViewModel>().AddSingleton<DesktopOverviewClockFormatter>().AddSingleton<DesktopOverviewClockController>().AddSingleton<DesktopPageLayoutCalculator>().AddSingleton<DesktopWallpaperPlacementCalculator>().AddSingleton<DesktopSnapLayoutCatalog>().AddSingleton<DesktopSnapPlacementResolver>().AddSingleton<DesktopSnapAppearanceCoordinator>().AddSingleton<DesktopWindowFrameGeometry>().AddSingleton<DesktopSnapSlotOccupancyResolver>().AddSingleton<DesktopWindowPlacementCoordinator>().AddSingleton<DesktopWindowDropNavigationCoordinator>().AddSingleton<DesktopWindowGroupDragCoordinator>().AddSingleton<DesktopWindowSelectionModel>().AddSingleton<DesktopPageArrangementCoordinator>().AddSingleton<DesktopApplicationPlacementResolver>().AddSingleton<DesktopApplicationLaunchCoordinator>().AddSingleton<DesktopApplicationDockContextMenuBuilder>().AddSingleton<DesktopApplicationDockPressAnimator>().AddSingleton<IDesktopApplicationPickerCatalog, DesktopApplicationPickerCatalog>().AddSingleton<IRecentApplicationStore, RecentApplicationStore>().AddSingleton<IDesktopApplicationPinStore, DesktopApplicationPinStore>().AddSingleton<IDesktopApplicationDockOrderStore, DesktopApplicationDockOrderStore>().AddSingleton<IDesktopApplicationDockCatalog, DesktopApplicationDockCatalog>().AddSingleton<DesktopDragBoundaryCalculator>().AddSingleton<DesktopPageReorderController>().AddSingleton<DesktopPageBackgroundFactory>().AddSingleton<DesktopPageStrip>().AddSingleton<DesktopOverviewDragScroller>().AddSingleton<DesktopWindowDragPageNavigator>().AddSingleton<DesktopDragCursorConfinement>().AddSingleton<DesktopWindowDragPositionResolver>().AddSingleton<DesktopWindowContextMenuBuilder>().AddSingleton<DesktopWindowPreviewFactory>().AddSingleton<DesktopWindowPreviewCollection>().AddSingleton<DesktopWindowGroupStackAnimator>().AddSingleton<DesktopOverviewInputController>().AddSingleton<DesktopWindowSnapInteractionCoordinator>().AddSingleton<DesktopOverviewLayoutPresenter>().AddSingleton<IDesktopOverviewSettingsNavigator, DesktopOverviewSettingsNavigator>().AddSingleton<DesktopOverviewSessionController>().AddSingleton<WindowInputTransparencyController>().AddSingleton(provider => new DesktopShortcutHintsViewModel(provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<Settings>(), provider.GetRequiredService<IKeyLabelProvider>())).AddSingleton<DesktopApplicationPickerViewModel>().AddSingleton<DesktopApplicationDockViewModel>().AddSingleton<PageTitleStore>().AddSingleton<PageLayoutStore>().AddSingleton<PageNavigationPublisher>().AddSingleton(provider => CreateDesktopScrollPreviewView(provider)).AddViewFor(ServiceLifetime.Singleton, provider => CreateDesktopOverviewView(provider), provider => CreateDesktopOverviewViewModel(provider)).AddView(ServiceLifetime.Singleton, provider => new ScrollTriggerView());
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
        services.Subscribe<DesktopSnapAppearanceCoordinator>((provider, coordinator) =>
        {
            coordinator.Start();
            return coordinator.Stop;
        });
        services.Subscribe<DesktopWallpaperPreloader>((provider, preloader) =>
        {
            preloader.Start();
            return preloader.Stop;
        });
    }


    private static DesktopScrollPreviewView CreateDesktopScrollPreviewView(IServiceProvider provider)
    {
        IWindowPreviewSurface windowPreviewSurface = provider.GetRequiredService<IWindowPreviewSurface>();
        IWindowCollection windowCollection = provider.GetRequiredService<IWindowCollection>();
        IPanState panState = provider.GetRequiredService<IPanState>();
        IPager pager = provider.GetRequiredService<IPager>();
        IScroller scroller = provider.GetRequiredService<IScroller>();
        IWorkspace workspace = provider.GetRequiredService<IWorkspace>();
        IScrollInputSuppression scrollInputSuppression = provider.GetRequiredService<IScrollInputSuppression>();
        IDesktopBackgroundSource backgroundSource = provider.GetRequiredService<IDesktopBackgroundSource>();
        DesktopOverviewConfiguration overviewConfiguration = provider.GetRequiredService<DesktopOverviewConfiguration>();
        DesktopOverviewForegroundThemeResolver foregroundThemeResolver = provider.GetRequiredService<DesktopOverviewForegroundThemeResolver>();
        DesktopScrollPreviewAnimator animator = provider.GetRequiredService<DesktopScrollPreviewAnimator>();
        DesktopOverviewChromeAnimator chromeAnimator = provider.GetRequiredService<DesktopOverviewChromeAnimator>();
        DesktopOverviewClockController clockController = provider.GetRequiredService<DesktopOverviewClockController>();
        DesktopOverviewLayoutPresenter layoutPresenter = provider.GetRequiredService<DesktopOverviewLayoutPresenter>();
        DesktopPageStrip pageStrip = provider.GetRequiredService<DesktopPageStrip>();
        DesktopWindowPreviewCollection previews = provider.GetRequiredService<DesktopWindowPreviewCollection>();
        DesktopDragCursorConfinement cursorConfinement = provider.GetRequiredService<DesktopDragCursorConfinement>();
        DesktopShortcutHintsViewModel shortcutHints = provider.GetRequiredService<DesktopShortcutHintsViewModel>();
        DesktopApplicationPickerViewModel applicationPicker = provider.GetRequiredService<DesktopApplicationPickerViewModel>();
        DesktopApplicationDockViewModel applicationDock = provider.GetRequiredService<DesktopApplicationDockViewModel>();
        DesktopApplicationDockContextMenuBuilder applicationDockContextMenuBuilder = provider.GetRequiredService<DesktopApplicationDockContextMenuBuilder>();
        DesktopApplicationDockPressAnimator applicationDockPressAnimator = provider.GetRequiredService<DesktopApplicationDockPressAnimator>();
        DesktopApplicationLaunchCoordinator applicationLaunchCoordinator = provider.GetRequiredService<DesktopApplicationLaunchCoordinator>();
        DesktopOverviewInputController inputController = provider.GetRequiredService<DesktopOverviewInputController>();
        DesktopWindowSnapInteractionCoordinator snapInteractionCoordinator = provider.GetRequiredService<DesktopWindowSnapInteractionCoordinator>();
        ILogger<DesktopScrollPreviewView> logger = provider.GetRequiredService<ILogger<DesktopScrollPreviewView>>();
        return new(windowPreviewSurface, windowCollection, panState, pager, scroller, workspace, scrollInputSuppression, backgroundSource, overviewConfiguration, foregroundThemeResolver, animator, chromeAnimator, clockController, layoutPresenter, pageStrip, previews, cursorConfinement, shortcutHints, applicationPicker, applicationDock, applicationDockContextMenuBuilder, applicationDockPressAnimator, applicationLaunchCoordinator, inputController, snapInteractionCoordinator, logger);
    }


    private static DesktopOverviewView CreateDesktopOverviewView(IServiceProvider provider)
    {
        DesktopScrollPreviewView desktopScrollPreview = provider.GetRequiredService<DesktopScrollPreviewView>();
        DesktopOverviewBackdropAnimator backdropAnimator = provider.GetRequiredService<DesktopOverviewBackdropAnimator>();
        DesktopOverviewWallpaperPresenter wallpaperPresenter = provider.GetRequiredService<DesktopOverviewWallpaperPresenter>();
        WindowInputTransparencyController inputController = provider.GetRequiredService<WindowInputTransparencyController>();
        IDesktopBackgroundSource backgroundSource = provider.GetRequiredService<IDesktopBackgroundSource>();
        IKeyboardInputSource keyboardInputSource = provider.GetRequiredService<IKeyboardInputSource>();
        IWindowEventListener windowEventListener = provider.GetRequiredService<IWindowEventListener>();
        DesktopOverviewConfiguration overviewConfiguration = provider.GetRequiredService<DesktopOverviewConfiguration>();
        return new(desktopScrollPreview, backdropAnimator, wallpaperPresenter, inputController, backgroundSource, keyboardInputSource, windowEventListener, overviewConfiguration);
    }


    private static DesktopOverviewViewModel CreateDesktopOverviewViewModel(IServiceProvider provider)
    {
        IServiceFactory serviceFactory = provider.GetRequiredService<IServiceFactory>();
        IMessenger messenger = provider.GetRequiredService<IMessenger>();
        IDisposer disposer = provider.GetRequiredService<IDisposer>();
        DesktopOverviewSessionController sessionController = provider.GetRequiredService<DesktopOverviewSessionController>();
        return new(provider, serviceFactory, messenger, disposer, sessionController);
    }
}
