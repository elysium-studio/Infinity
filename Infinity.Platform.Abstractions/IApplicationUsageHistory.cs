namespace Infinity.Platform.Abstractions;

public interface IApplicationUsageHistory
{
    Task<IReadOnlyList<LaunchableApplication>> GetRecentlyUsedApplicationsAsync(
        IReadOnlyList<LaunchableApplication> applications,
        int maximumCount,
        CancellationToken cancellationToken = default);
}
