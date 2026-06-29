using Elysium.Application;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Elysium.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using System;

namespace Infinity.Shell.WinUI;

public class ApplicationModule(string applicationData,
    DispatcherQueue dispatcherQueue,
    Action<Action<Exception?>> registerFlushHandler) :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddExceptionLogging(builder =>
            {
                builder.WithAppDomainHandler();
                builder.WithTaskSchedulerHandler();

                registerFlushHandler(builder.CreateFlushHandler());
            })
            .AddSingleton(new AppEnvironment(applicationData))
            .AddSingleton<IStartupManager>(new StartupManager(Environment.ProcessPath ?? string.Empty, "InfinityDesktop", "InfinityDesktop"))
            .AddSingleton<IDispatcher>(new Dispatcher(args => dispatcherQueue.TryEnqueue(() => args())));
    }
}