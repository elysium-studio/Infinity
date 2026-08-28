using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopApplicationPickerCatalog(IApplicationCatalog applicationCatalog) : IDesktopApplicationPickerCatalog
{
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private IReadOnlyList<LaunchableApplication>? applications;

    public async Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default)
    {
        if (applications is not null)
        {
            return applications;
        }

        await loadGate.WaitAsync(cancellationToken);

        try
        {
            applications ??= [.. await applicationCatalog.GetApplicationsAsync(cancellationToken)];
            return applications;
        }
        finally
        {
            loadGate.Release();
        }
    }

    public Task<ApplicationIcon?> GetIconAsync(LaunchableApplication application, CancellationToken cancellationToken = default) =>
        applicationCatalog.GetIconAsync(application, cancellationToken);
}
