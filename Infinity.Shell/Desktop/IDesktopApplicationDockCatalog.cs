using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public interface IDesktopApplicationDockCatalog
{
    Task<IReadOnlyList<DesktopApplicationDockEntry>> GetApplicationsAsync(
        int maximumCount,
        CancellationToken cancellationToken = default);
}
