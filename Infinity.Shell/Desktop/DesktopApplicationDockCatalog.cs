using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Shell;

public sealed class DesktopApplicationDockCatalog(IDesktopApplicationPickerCatalog applicationCatalog, ITaskbarPinnedApplicationSource taskbarPins, IDesktopApplicationPinStore infinityPins, IDesktopApplicationDockOrderStore orderStore, ILogger<DesktopApplicationDockCatalog> logger) : IDesktopApplicationDockCatalog
{
    public async Task<IReadOnlyList<DesktopApplicationDockEntry>> GetApplicationsAsync(int maximumCount, CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0)
        {
            return[];
        }

        IReadOnlyList<LaunchableApplication> availableApplications = await applicationCatalog.GetApplicationsAsync(cancellationToken);
        Task<IReadOnlyList<LaunchableApplication>> taskbarTask = GetTaskbarPinsAsync(availableApplications, cancellationToken);
        List<DesktopApplicationDockEntry> result = new(maximumCount);
        HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<LaunchableApplication> infinityApplications = infinityPins.Applications;
        int taskbarLimit = Math.Max(0, maximumCount - Math.Min(infinityApplications.Count, maximumCount));
        AddDistinct((await taskbarTask).Take(taskbarLimit), DesktopApplicationDockSource.Taskbar, result, identifiers, maximumCount);
        AddDistinct(infinityApplications, DesktopApplicationDockSource.Infinity, result, identifiers, maximumCount);
        return DesktopApplicationDockOrderer.Apply(result, orderStore.ApplicationIdentifiers);
    }


    private async Task<IReadOnlyList<LaunchableApplication>> GetTaskbarPinsAsync(IReadOnlyList<LaunchableApplication> availableApplications, CancellationToken cancellationToken)
    {
        try
        {
            return await taskbarPins.GetPinnedApplicationsAsync(availableApplications, cancellationToken);
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "The taskbar pinned application list could not be loaded");
            return[];
        }
    }


    private static void AddDistinct(IEnumerable<LaunchableApplication> applications, DesktopApplicationDockSource source, ICollection<DesktopApplicationDockEntry> result, ISet<string> identifiers, int maximumCount)
    {
        foreach (LaunchableApplication application in applications)
        {
            if (result.Count >= maximumCount)
            {
                return;
            }

            if (identifiers.Add(application.Id))
            {
                result.Add(new DesktopApplicationDockEntry(application, source));
            }
        }
    }
}
