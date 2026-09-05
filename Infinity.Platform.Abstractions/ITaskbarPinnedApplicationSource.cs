namespace Infinity.Platform.Abstractions;

public interface ITaskbarPinnedApplicationSource
{
    Task<IReadOnlyList<LaunchableApplication>> GetPinnedApplicationsAsync(IReadOnlyList<LaunchableApplication> availableApplications, CancellationToken cancellationToken = default);
}
