using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public interface IDesktopApplicationDockCatalog
{
    Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(
        int maximumCount,
        CancellationToken cancellationToken = default);
}
