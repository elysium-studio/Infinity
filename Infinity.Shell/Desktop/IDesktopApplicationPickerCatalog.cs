using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public interface IDesktopApplicationPickerCatalog
{
    Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default);

    Task<ApplicationIcon?> GetIconAsync(LaunchableApplication application, CancellationToken cancellationToken = default);
}
