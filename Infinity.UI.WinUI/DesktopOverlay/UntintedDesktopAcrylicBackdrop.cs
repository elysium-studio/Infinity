using System.Collections.Generic;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Infinity.UI.WinUI;

public partial class UntintedDesktopAcrylicBackdrop : SystemBackdrop
{
    private readonly Dictionary<ICompositionSupportsSystemBackdrop, DesktopAcrylicController> controllers = [];

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        SystemBackdropConfiguration configuration = GetDefaultSystemBackdropConfiguration(target, xamlRoot);
        configuration.IsInputActive = true;
        DesktopAcrylicController controller = new()
        {
            LuminosityOpacity = 0,
            TintOpacity = 0
        };
        controller.AddSystemBackdropTarget(target);
        controller.SetSystemBackdropConfiguration(configuration);
        controllers.Add(target, controller);
    }


    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnDefaultSystemBackdropConfigurationChanged(target, xamlRoot);
        SystemBackdropConfiguration configuration = GetDefaultSystemBackdropConfiguration(target, xamlRoot);
        configuration.IsInputActive = true;
        if (controllers.TryGetValue(target, out DesktopAcrylicController? controller))
        {
            controller.SetSystemBackdropConfiguration(configuration);
        }
    }


    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        base.OnTargetDisconnected(target);
        if (controllers.Remove(target, out DesktopAcrylicController? controller))
        {
            controller.RemoveSystemBackdropTarget(target);
            controller.Dispose();
        }
    }
}
