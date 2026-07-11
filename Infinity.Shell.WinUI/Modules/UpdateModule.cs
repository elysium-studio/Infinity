using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.UI.WinUI;
using Elysium.Updates.Abstractions;
using Elysium.Updates.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.IO;
using System.Threading.Tasks;
using Velopack.Locators;

namespace Infinity.Shell.WinUI;

public class UpdateModule :
    IModule
{
    private const string RestartForUpdateArgument = "update=restart";
    private const string DismissUpdateArgument = "update=dismiss";

    public void Register(IServiceCollection services)
    {
        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        services.AddSingleton(provider => new AppToastNotifier(
            VelopackLocator.Current.AppUserModelId ?? throw new InvalidOperationException("Velopack did not provide an AppUserModelID."),
            "Infinity",
            Path.Combine(AppContext.BaseDirectory, "Assets", "Infinity.ico")));

        // Subscribe before AddUpdateController registers the hosted update monitor so a fast update check cannot
        // publish UpdateReady before the notification handler is attached.
        services.Subscribe<IUpdateController>((provider, controller) =>
        {
            ILogger<UpdateModule> logger = provider.GetRequiredService<ILogger<UpdateModule>>();
            Task? restartTask = null;

            void HandleUpdateReady(string version)
            {
                bool enqueued = dispatcherQueue.TryEnqueue(() =>
                {
                    try
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
                            if (argument != RestartForUpdateArgument)
                            {
                                return;
                            }

                            bool restartEnqueued = dispatcherQueue.TryEnqueue(() =>
                            {
                                if (restartTask is { IsCompleted: false })
                                {
                                    return;
                                }

                                restartTask = ApplyUpdateAndExitAsync(provider, controller, logger);
                            });

                            if (!restartEnqueued)
                            {
                                logger.LogWarning("Dispatcher rejected update restart request");
                            }
                        });

                        logger.LogInformation("Displayed update-ready notification for version {Version}", version);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Failed to display update-ready notification for version {Version}", version);
                    }
                });

                if (!enqueued)
                {
                    logger.LogWarning("Dispatcher rejected update-ready notification for version {Version}", version);
                }
            }

            controller.UpdateReady += HandleUpdateReady;
            return () => controller.UpdateReady -= HandleUpdateReady;
        });

        services.AddUpdateController(configuration =>
        {
            configuration.FeedUrl = "https://elysiumstud.io/feeds/infinity";
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
