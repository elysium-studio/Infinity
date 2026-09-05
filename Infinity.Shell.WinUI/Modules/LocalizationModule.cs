using Elysium.Application.DependencyInjection;
using Elysium.UI.WinUI;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infinity.Shell.WinUI;

public sealed class LocalizationModule : IModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IStringLocalizer, ResourceStringLocalizer>();
        services.AddSingleton<ITextLocalizer, ResourceTextLocalizer>();
        services.Subscribe<IStringLocalizer>((provider, localizer) =>
        {
            LocalizeExtension.SetLocalizer(localizer);
            return () => LocalizeExtension.SetLocalizer(null);
        });
    }
}
