using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Shell;

public sealed class DesktopApplicationDockCatalog(
    IDesktopApplicationPickerCatalog applicationCatalog,
    IApplicationUsageHistory usageHistory,
    IRecentApplicationStore infinityHistory,
    ILogger<DesktopApplicationDockCatalog> logger) :
    IDesktopApplicationDockCatalog
{
    public async Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0)
        {
            return [];
        }

        IReadOnlyList<LaunchableApplication> windowsHistory;

        try
        {
            IReadOnlyList<LaunchableApplication> applications =
                await applicationCatalog.GetApplicationsAsync(cancellationToken);
            windowsHistory =
                await usageHistory.GetRecentlyUsedApplicationsAsync(applications, maximumCount, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "The Windows recent application list could not be loaded");
            windowsHistory = [];
        }

        List<LaunchableApplication> result = new(maximumCount);
        HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);

        AddDistinct(windowsHistory, result, identifiers, maximumCount);
        AddDistinct(infinityHistory.Applications, result, identifiers, maximumCount);

        return result;
    }

    private static void AddDistinct(
        IEnumerable<LaunchableApplication> applications,
        ICollection<LaunchableApplication> result,
        ISet<string> identifiers,
        int maximumCount)
    {
        foreach (LaunchableApplication application in applications)
        {
            if (result.Count >= maximumCount)
            {
                return;
            }

            if (identifiers.Add(application.Id))
            {
                result.Add(application);
            }
        }
    }
}
