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
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IApplicationLifetime = Elysium.Application.Abstractions.IApplicationLifetime;

namespace Infinity.Shell.WinUI;

public sealed partial class App
{
    private readonly Lock shutdownLock = new();

    private DispatcherQueue? dispatcherQueue;
    private IHost? host;
    private Task? shutdownTask;
    private Task? startupNavigationTask;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string applicationData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Infinity");

        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        IHost? startingHost = null;
        ILogger<App>? logger = null;

        try
        {
            startingHost = Host.CreateDefaultBuilder()
                .UseWritableContentRoot(applicationData)
                .ConfigureServices(services => services
                    .AddSingleton<IApplicationLifetime>(new ApplicationLifetime(ShutdownAsync))
                    .AddInfinityApplication()
                    .AddInfinityPlatform()
                    .AddApplication()
                    .AddPresentation()
                    .AddModules(new ApplicationModule(applicationData, dispatcherQueue,
                                    flush => UnhandledException += (_, args) => flush(args.Exception)),
                                new ConfigurationModule(),
                                new LocalizationModule(),
                                new NavigationModule(),
                                new DesktopSettingsModule(),
                                new DesktopModule(),
                                new PreviewSettingsModule(),
                                new ShellModule(),
                                new SettingsModule(),
                                new TourModule(),
                                new UpdateModule(),
                                new WindowsSettingsModule(),
                                new WindowingModule()))
                .Build();

            host = startingHost;
            logger = startingHost.Services.GetRequiredService<ILogger<App>>();
            ViewExtension.DefaultProvider = startingHost.Services;
            ViewModelExtension.DefaultProvider = startingHost.Services;

            _ = startingHost.Services.GetRequiredKeyedService<DesktopFlyoutView>("DesktopFlyoutView");
            _ = startingHost.Services.GetRequiredKeyedService<PageTintView>("PageTintView");

            startingHost.Start();

            if (startingHost.Services.GetRequiredService<Settings>() is { ShowHintOnStartup: true })
            {
                startupNavigationTask = NavigateToStartupTourAsync(startingHost.Services.GetRequiredService<INavigator>(),
                    logger);
            }
        }
        catch (Exception exception)
        {
            logger?.LogCritical(exception, "Infinity failed to start");

            try
            {
                startingHost?.Dispose();
            }
            catch (Exception cleanupException)
            {
                logger?.LogError(cleanupException, "Infinity startup cleanup failed");
            }

            host = null;
            throw;
        }
    }

    private Task ShutdownAsync()
    {
        lock (shutdownLock)
        {
            return shutdownTask ??= ShutdownCoreAsync();
        }
    }

    private async Task ShutdownCoreAsync()
    {
        IHost? currentHost = host;

        if (currentHost is not null)
        {
            try
            {
                if (startupNavigationTask is not null)
                {
                    await startupNavigationTask;
                }

                await currentHost.StopAsync();
            }
            finally
            {
                await CompleteShutdownAsync(currentHost);
            }

            return;
        }

        Current.Exit();
    }

    private Task CompleteShutdownAsync(IHost currentHost)
    {
        DispatcherQueue currentDispatcherQueue = dispatcherQueue
            ?? throw new InvalidOperationException("The application dispatcher is not available");

        if (currentDispatcherQueue.HasThreadAccess)
        {
            CompleteShutdown(currentHost);
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!currentDispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                CompleteShutdown(currentHost);
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("The application dispatcher rejected the shutdown request"));
        }

        return completion.Task;
    }

    private void CompleteShutdown(IHost currentHost)
    {
        try
        {
            currentHost.Dispose();
        }
        finally
        {
            host = null;
            Current.Exit();
        }
    }

    private static async Task NavigateToStartupTourAsync(INavigator navigator, ILogger logger)
    {
        try
        {
            await navigator.NavigateAsync("TourWindow");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to navigate to the startup tour");
        }
    }
}
