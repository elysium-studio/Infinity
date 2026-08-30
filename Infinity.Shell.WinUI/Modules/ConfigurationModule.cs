using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Windowing;
using System;
using System.Text.Json;

namespace Infinity.Shell.WinUI;

public sealed class ConfigurationModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        WritableOptionsBuilder<Settings> builder = new(services, "Settings", "settings.dat");

        builder.WithJsonOptions(new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolverChain = { InfinityJsonContext.Default }
        })
            .UseJson()
            .WithChangeHandler((provider, options, name) =>
                provider.GetRequiredService<IMessenger>()
                    .Send(new OptionsChangedEventArgs<Settings>(options)))
            .WithChangeHandler((provider, options, _) =>
                provider.GetRequiredService<ScrollerConfiguration>()
                    .PixelsPerScrollNotch = options.ScrollSpeed.ToPixelsPerNotch())
            .WithChangeHandler((provider, options, _) =>
                provider.GetRequiredService<IModifierKeyState>()
                    .SetKeys(options.ScrollModifierKeys))
            .WithAsyncChangeHandler(async (provider, options, _) =>
            {
                IStartupManager startupManager = provider.GetRequiredService<IStartupManager>();

                if (options.StartWithWindows)
                {
                    await startupManager.EnableAsync();
                }
                else
                {
                    await startupManager.DisableAsync();
                }
            })
            .WithChangeHandler((provider, options, _) =>
            {
                int? maxPages = options.VirtualPagesMode == VirtualPagesMode.Fixed
                    ? (int?)options.VirtualPagesCount
                    : null;

                provider.GetRequiredService<IPager>()
                    .SetMaxPages(maxPages);

                provider.GetRequiredService<IPanState>()
                    .SetMaxOffset(maxPages.HasValue ? (maxPages.Value - 1) * (double)DisplayArea.Primary.WorkArea.Width : double.MaxValue);
            })
            .WithChangeHandler((provider, options, _) =>
                provider.GetRequiredService<WindowDragScrollerConfiguration>()
                    .SpeedLevel = options.DragScrollSpeed)
            .WithChangeHandler((provider, options, _) =>
            {
                DesktopOverviewConfiguration configuration = provider.GetRequiredService<DesktopOverviewConfiguration>();

                configuration.Backdrop = options.OverviewBackdrop;
                configuration.IsEdgeScrollingEnabled = options.EnableOverviewEdgeScrolling;
                configuration.IsMonitorSpanningEnabled = options.SpanCompatibleDisplays;
                configuration.IsSnapAssistanceEnabled = options.EnableSnapAssistance;
            });

        services
            .AddSingleton(provider =>
                new ScrollerConfiguration
                {
                    PixelsPerScrollNotch = provider.GetRequiredService<Settings>().ScrollSpeed.ToPixelsPerNotch()
                })
            .AddSingleton(provider =>
                new WindowDragScrollerConfiguration
                {
                    SpeedLevel = provider.GetRequiredService<Settings>().DragScrollSpeed
                })
            .AddSingleton(provider =>
            {
                Settings settings = provider.GetRequiredService<Settings>();

                return new DesktopOverviewConfiguration
                {
                    Backdrop = settings.OverviewBackdrop,
                    IsEdgeScrollingEnabled = settings.EnableOverviewEdgeScrolling,
                    IsMonitorSpanningEnabled = settings.SpanCompatibleDisplays,
                    IsSnapAssistanceEnabled = settings.EnableSnapAssistance
                };
            })
            .AddSingleton<Func<ScrollerConfiguration>>(provider =>
                () => provider.GetRequiredService<ScrollerConfiguration>())
            .AddSingleton<Func<WindowDragScrollerConfiguration>>(provider =>
                () => provider.GetRequiredService<WindowDragScrollerConfiguration>())
            .AddSingleton<IConfiguration>(provider =>
            {
                IConfigurationBuilder configBuilder = new ConfigurationBuilder()
                    .SetBasePath(provider.GetRequiredService<IHostEnvironment>().ContentRootPath)
                    .AddJsonFile("settings.dat", optional: true, reloadOnChange: true);

                return configBuilder.Build();
            });
    }
}
