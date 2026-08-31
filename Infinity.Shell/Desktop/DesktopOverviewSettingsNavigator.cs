using Elysium.Presentation.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Shell;

public sealed class DesktopOverviewSettingsNavigator(INavigator navigator, ILogger<DesktopOverviewSettingsNavigator> logger) : IDesktopOverviewSettingsNavigator
{
    public async Task NavigateAsync()
    {
        try
        {
            await navigator.NavigateAsync("SettingsWindow");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to navigate to Settings");
        }
    }
}
