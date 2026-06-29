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

public class ConfigurationModule :
    IModule
{
    public void Register(IServiceCollection services) => services
            .AddWritableOptions<Settings>("Settings", "settings.dat",
                builder =>
                {
                    builder.WithJsonOptions(new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true,
                        TypeInfoResolverChain = { InfinityJsonContext.Default }
                    });

                    builder.UseJson();

                    builder.WithChangeHandler((provider, options, name) =>
                        provider.GetRequiredService<IMessenger>()
                            .Send(new OptionsChangedEventArgs<Settings>(options)));

                    builder.WithChangeHandler((provider, options, _) =>
                        provider.GetRequiredService<ScrollerConfiguration>()
                            .PixelsPerScrollNotch = options.ScrollSpeed.ToPixelsPerNotch());

                    builder.WithChangeHandler((provider, options, _) =>
                        provider.GetRequiredService<IModifierKeyState>()
                            .SetKeys(options.ScrollModifierKeys));

                    builder.WithChangeHandler((provider, options, _) =>
                    {
                        IStartupManager startupManager = provider.GetRequiredService<IStartupManager>();

                        if (options.StartWithWindows)
                        {
                            startupManager.Enable();
                        }
                        else
                        {
                            startupManager.Disable();
                        }
                    });

                    builder.WithChangeHandler((provider, options, _) =>
                    {
                        int? maxPages = options.VirtualPagesMode == VirtualPagesMode.Fixed
                            ? (int?)options.VirtualPagesCount
                            : null;

                        provider.GetRequiredService<IPager>()
                            .SetMaxPages(maxPages);

                        provider.GetRequiredService<IPanState>()
                            .SetMaxOffset(maxPages.HasValue ? (maxPages.Value - 1) * (double)DisplayArea.Primary.WorkArea.Width : double.MaxValue);
                    });

                    builder.WithChangeHandler((provider, options, _) =>
                        provider.GetRequiredService<WindowDragScrollerConfiguration>()
                            .SpeedLevel = options.DragScrollSpeed);
                })
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