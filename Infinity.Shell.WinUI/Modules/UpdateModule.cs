using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.UI.WinUI;
using Elysium.Updates.Abstractions;
using Elysium.Updates.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace Infinity.Shell.WinUI;

public sealed class UpdateModule :
    IModule
{
    private const string RestartForUpdateArgument = "update=restart";
    private const string DismissUpdateArgument = "update=dismiss";

    public void Register(IServiceCollection services)
    {
        if (PackageIdentity.IsPackaged)
        {
            return;
        }

        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        services.AddUpdateController(configuration =>
        {
            configuration.FeedUrl = "https://elysiumstud.io/feeds/infinity";
        });

        services.AddSingleton<AppToastNotifier>();

        services.Subscribe<IUpdateController>((provider, controller) =>
        {
            ILogger<UpdateModule> logger = provider.GetRequiredService<ILogger<UpdateModule>>();

            void HandleUpdateReady(string version)
            {
                bool enqueued = dispatcherQueue.TryEnqueue(() =>
                {
                    IStringLocalizer localizer = provider.GetRequiredService<IStringLocalizer>();

                    ToastContent content = new ToastBuilder()
                        .AddText(localizer.GetString("UpdateReadyToastTitle"))
                        .AddText(localizer.GetString("UpdateReadyToastDownloaded", version))
                        .AddText(localizer.GetString("UpdateReadyToastRestartRequired"))
                        .SetLaunchArgument(RestartForUpdateArgument)
                        .AddButton(localizer.GetString("UpdateReadyToastRestartButton"), RestartForUpdateArgument)
                        .AddButton(localizer.GetString("UpdateReadyToastDismissButton"), DismissUpdateArgument)
                        .Build();

                    provider.GetRequiredService<AppToastNotifier>().Show(content, argument =>
                    {
                        if (argument == RestartForUpdateArgument)
                        {
                            bool restartEnqueued = dispatcherQueue.TryEnqueue(() =>
                            {
                                _ = ApplyUpdateAndExitAsync(provider, controller, logger);
                            });

                            if (!restartEnqueued)
                            {
                                logger.LogWarning("Dispatcher rejected update restart request");
                            }
                        }
                    });
                });

                if (!enqueued)
                {
                    logger.LogWarning("Dispatcher rejected update-ready notification for version {Version}", version);
                }
            }

            controller.UpdateReady += HandleUpdateReady;
            return () => controller.UpdateReady -= HandleUpdateReady;
        });
    }

    private static async Task ApplyUpdateAndExitAsync(IServiceProvider provider,
        IUpdateController controller,
        ILogger logger)
    {
        try
        {
            controller.ApplyOnExit();
            await provider.GetRequiredService<IApplicationLifetime>().ExitAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to restart for update");
        }
    }
}
