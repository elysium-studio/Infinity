namespace Infinity.Platform.Abstractions;

public interface IApplicationUsageHistory
{
    Task<IReadOnlyList<LaunchableApplication>> GetMostUsedApplicationsAsync(IReadOnlyList<LaunchableApplication> applications, int maximumCount, CancellationToken cancellationToken = default);
}
