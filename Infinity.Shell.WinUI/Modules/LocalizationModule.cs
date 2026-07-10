using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Infinity.Shell.WinUI;

public class LocalizationModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IStringLocalizer, ResourceStringLocalizer>();

        services.Subscribe<IStringLocalizer>((provider, localizer) =>
        {
            LocalizeExtension.SetLocalizer(localizer);
            return () => LocalizeExtension.SetLocalizer(null);
        });
    }
}