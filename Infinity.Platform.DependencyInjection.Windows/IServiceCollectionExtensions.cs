using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Platform.Windows;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infinity.Platform.Windows.DependencyInjection;

public static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfinityPlatform()
        {
            services.AddSingleton<IMessageWindow, MessageWindow>();
            services.AddSingleton<IWorkspace, Workspace>();
            services.AddSingleton<IDesktopBackgroundSource, DesktopBackgroundSource>();

            services.AddSingleton(new HotKeysBuilderOptions
            {
                KeyCount = 2
            });

            services.AddTransient<IHotKeysBuilder, HotKeysBuilder>();
            services.AddSingleton<IKeyLabelProvider, KeyLabelProvider>();

            services.AddSingleton<IKeyboardInputSource>(provider =>
                new KeyboardInputSource(provider.GetRequiredService<ILogger<KeyboardInputSource>>()));

            services.AddSingleton<IMouseInputSource>(provider =>
                new MouseInputSource(provider.GetRequiredService<ILogger<MouseInputSource>>()));

            services.AddSingleton<IModifierKeyState>(provider =>
                new ModifierKeyState(provider.GetRequiredService<IKeyboardInputSource>(),
                    provider.GetRequiredService<IKeyboardInputKeysFactory>().Create()));

            services.AddSingleton<IPointerInputSource, PointerInputSource>();
            services.AddSingleton<IForegroundWindowSource, ForegroundWindowSource>();

            services.AddSingleton<IWindowController, WindowController>();
            services.AddSingleton<IWindowActivator, WindowActivator>();
            services.AddSingleton<IWindowAncestorResolver, WindowAncestorResolver>();
            services.AddSingleton<IWindowEnumerator, WindowEnumerator>();
            services.AddSingleton<IWindowGeometryReader, WindowGeometryReader>();
            services.AddSingleton<IWindowMover, WindowMover>();
            services.AddSingleton<IWindowOpacity, WindowOpacity>();
            services.AddSingleton<IWindowPreviewSurface, DwmWindowPreviewSurface>();
            services.AddSingleton<IWindowTitleReader, WindowTitleReader>();

            services.AddSingleton<IWindowFocusGuard, WindowFocusGuard>();
            services.AddSingleton<IWindowDragGuard, WindowDragGuard>();
            services.AddSingleton<IWindowMoveGuard, WindowMoveGuard>();

            services.AddSingleton<IMonitorLocator, MonitorLocator>();
            services.AddSingleton<ITaskbarLocator, TaskbarLocator>();

            services.AddSingleton<WindowFilterOptions>();
            services.AddSingleton<IWindowFilter, WindowFilter>();

            services.AddSingleton<IWindowZOrder>(provider =>
                new WindowZOrder(provider.GetRequiredService<IWindowStore>(),
                    provider.GetRequiredService<IWindowEventListener>(),
                    provider.GetRequiredService<IWindowFocusGuard>(),
                    () => provider.GetRequiredService<IMessageWindow>().Handle,
                    provider.GetRequiredService<ILogger<WindowZOrder>>()));

            services.AddSingleton<IWindowEventListener>(provider =>
                new WindowEventListener(provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<IMessageWindow>().Handle,
                    provider.GetRequiredService<ILogger<WindowEventListener>>()));

            return services;
        }
    }
}