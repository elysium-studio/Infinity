using Elysium.Application;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Infinity.Application.DependencyInjection;
using Infinity.Platform.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using IApplicationLifetime = Elysium.Application.Abstractions.IApplicationLifetime;

namespace Infinity.Shell.WinUI;

public partial class App
{
    private const string RestartForUpdateArgument = "update=restart";
    private const string DismissUpdateArgument = "update=dismiss";

    private IHost? host;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string applicationData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Infinity");

        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        host = Host.CreateDefaultBuilder()
            .UseWritableContentRoot(applicationData)
            .ConfigureServices(services => services
                .AddSingleton<IApplicationLifetime>(new ApplicationLifetime(async () =>
                {
                    if (host is not null)
                    {
                        await host.StopAsync();
                        host.Dispose();
                    }

                    Current.Exit();
                }))
                .AddInfinityApplication()
                .AddInfinityPlatform()
                .AddApplication()
                .AddPresentation()
                .AddModules(new ApplicationModule(applicationData, dispatcherQueue,
                                flush => UnhandledException += (_, args) => flush(args.Exception)),
                            new ConfigurationModule(),
                            new NavigationModule(),
                            new ShellModule(),
                            new SettingsModule(),
                            new TourModule(),
                            new UpdateModule()))
            .Build();

        ViewExtension.DefaultProvider = host.Services;
        ViewModelExtension.DefaultProvider = host.Services;

        _ = host.Services.GetRequiredKeyedService<DesktopFlyoutView>("DesktopFlyoutView");
        _ = host.Services.GetRequiredKeyedService<PageTintView>("PageTintView");

        host.Start();

        ToastContent content = new ToastBuilder()
            .AddText("Update ready")
            .AddText("Infinity update test.")
            .AddText("Restart to finish updating.")
            .SetLaunchArgument(RestartForUpdateArgument)
            .AddButton("Restart", RestartForUpdateArgument)
            .AddButton("Dismiss", DismissUpdateArgument)
            .Build();

        if (host.Services.GetRequiredService<Settings>() is { ShowHintOnStartup: true })
        {
            if (host.Services.GetRequiredService<INavigator>() is Navigator navigator)
            {
                _ = navigator.NavigateAsync("TourWindow");
            }
        }
    }
}