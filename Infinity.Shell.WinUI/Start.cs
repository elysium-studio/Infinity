using Elysium.Platform.Windows;
using System;
using Velopack;

namespace Infinity.Shell.WinUI;

public static class Start
{
    [STAThread]
    public static void Main()
    {
        using SingleInstanceGuard? instanceGuard = SingleInstanceGuard.TryAcquire($"{Environment.UserName}.Infinity");

        if (instanceGuard is null)
        {
            return;
        }

        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(UninstallCleanup.Run)
            .Run();

#pragma warning disable CA1806
        Microsoft.UI.Xaml.Application.Start(args => new App());
#pragma warning restore CA1806
    }
}