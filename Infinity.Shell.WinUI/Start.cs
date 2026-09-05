using System;
using Elysium.Platform.Windows;
using Velopack;
using XamlApplication = Microsoft.UI.Xaml.Application;

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

        if (!PackageIdentity.IsPackaged)
        {
            VelopackApp.Build().OnBeforeUninstallFastCallback(UninstallCleanup.Run).Run();
        }

#pragma warning disable CA1806
        XamlApplication.Start(args => new App());
#pragma warning restore CA1806
    }
}
