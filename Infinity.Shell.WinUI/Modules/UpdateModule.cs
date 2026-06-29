using Elysium.Application.DependencyInjection;
using Elysium.UI.WinUI;
using Elysium.Updates.Abstractions;
using Elysium.Updates.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace Infinity.Shell.WinUI;

public class UpdateModule :
    IModule
{
    private const string RestartForUpdateArgument = "update=restart";
    private const string DismissUpdateArgument = "update=dismiss";

    public void Register(IServiceCollection services)
    {
        services.AddUpdateController(configuration =>
        {
            configuration.FeedUrl = "https://elysiumstud.io/feeds/infinity";
        });

        services.AddSingleton(provider => new AppToastNotifier("ElysiumStudio.Infinity", "Infinity",
            Path.Combine(AppContext.BaseDirectory, "Assets", "Infinity.ico")));

        services.Subscribe<IUpdateController>((provider, controller) =>
        {
            void HandleUpdateReady(string version)
            {
                ToastContent content = new ToastBuilder()
                    .AddText("Update ready")
                    .AddText($"Infinity {version} has been downloaded.")
                    .AddText("Restart to finish updating.")
                    .SetLaunchArgument(RestartForUpdateArgument)
                    .AddButton("Restart", RestartForUpdateArgument)
                    .AddButton("Dismiss", DismissUpdateArgument)
                    .Build();

                provider.GetRequiredService<AppToastNotifier>().Show(content, argument =>
                {
                    if (argument == RestartForUpdateArgument)
                    {
                        IUpdateController controller = provider.GetRequiredService<IUpdateController>();

                        if (controller.ApplyAndRestart())
                        {
                            Environment.Exit(0);
                        }
                    }
                });
            }

            controller.UpdateReady += HandleUpdateReady;
            return () => controller.UpdateReady -= HandleUpdateReady;
        });
    }
}