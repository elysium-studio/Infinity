namespace Infinity.Platform.Abstractions;

public interface IApplicationCatalog
{
    Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default);
}
