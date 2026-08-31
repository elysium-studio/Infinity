using Infinity.Platform.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class DesktopApplicationDockCatalogTests
{
    [Fact]
    public async Task WindowsHistoryIsPreferredAndInfinityHistoryFillsRemainingPlaces()
    {
        LaunchableApplication calculator = new("calculator", "Calculator");
        LaunchableApplication paint = new("paint", "Paint");
        LaunchableApplication terminal = new("terminal", "Terminal");
        DesktopApplicationDockCatalog catalog = new(
            new TestPickerCatalog([calculator, paint, terminal]),
            new TestUsageHistory([paint, calculator]),
            new TestRecentApplicationStore([calculator, terminal]),
            NullLogger<DesktopApplicationDockCatalog>.Instance);

        IReadOnlyList<LaunchableApplication> applications = await catalog.GetApplicationsAsync(3);

        Assert.Equal([paint, calculator, terminal], applications);
    }

    private sealed class TestPickerCatalog(IReadOnlyList<LaunchableApplication> applications) :
        IDesktopApplicationPickerCatalog
    {
        public Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(applications);

        public Task<ApplicationIcon?> GetIconAsync(
            LaunchableApplication application,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ApplicationIcon?>(null);
    }

    private sealed class TestUsageHistory(IReadOnlyList<LaunchableApplication> applications) :
        IApplicationUsageHistory
    {
        public Task<IReadOnlyList<LaunchableApplication>> GetRecentlyUsedApplicationsAsync(
            IReadOnlyList<LaunchableApplication> availableApplications,
            int maximumCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LaunchableApplication>>([.. applications.Take(maximumCount)]);
    }

    private sealed class TestRecentApplicationStore(IReadOnlyList<LaunchableApplication> applications) :
        IRecentApplicationStore
    {
        public event Action<LaunchableApplication>? ApplicationRecorded;

        public IReadOnlyList<LaunchableApplication> Applications => applications;

        public Task RecordAsync(LaunchableApplication application, CancellationToken cancellationToken = default)
        {
            ApplicationRecorded?.Invoke(application);
            return Task.CompletedTask;
        }

        public void RecordForSession(LaunchableApplication application) => ApplicationRecorded?.Invoke(application);
    }
}
