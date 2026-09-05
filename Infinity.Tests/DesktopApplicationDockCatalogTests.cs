using Infinity.Platform.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class DesktopApplicationDockCatalogTests
{
    [Fact]
    public async Task DockContainsOnlyTaskbarAndInfinityPins()
    {
        LaunchableApplication calculator = new("calculator", "Calculator");
        LaunchableApplication paint = new("paint", "Paint");
        LaunchableApplication terminal = new("terminal", "Terminal");
        LaunchableApplication edge = new("edge", "Microsoft Edge");
        DesktopApplicationDockCatalog catalog = new(new TestPickerCatalog([calculator, paint, terminal, edge]), new TestTaskbarPins([edge]), new TestPinStore([terminal]), new TestOrderStore([]), NullLogger<DesktopApplicationDockCatalog>.Instance);
        IReadOnlyList<DesktopApplicationDockEntry> applications = await catalog.GetApplicationsAsync(4);
        Assert.Equal([edge, terminal], applications.Select(entry => entry.Application));
        Assert.Equal([DesktopApplicationDockSource.Taskbar, DesktopApplicationDockSource.Infinity], applications.Select(entry => entry.Source));
    }


    [Fact]
    public async Task InfinityPinsReserveDockSpaceWhenTaskbarIsFull()
    {
        LaunchableApplication edge = new("edge", "Edge");
        LaunchableApplication explorer = new("explorer", "File Explorer");
        LaunchableApplication terminal = new("terminal", "Terminal");
        LaunchableApplication calculator = new("calculator", "Calculator");
        DesktopApplicationDockCatalog catalog = new(new TestPickerCatalog([edge, explorer, terminal, calculator]), new TestTaskbarPins([edge, explorer]), new TestPinStore([terminal, calculator]), new TestOrderStore([]), NullLogger<DesktopApplicationDockCatalog>.Instance);
        IReadOnlyList<DesktopApplicationDockEntry> applications = await catalog.GetApplicationsAsync(3);
        Assert.Equal([edge, terminal, calculator], applications.Select(entry => entry.Application));
    }


    [Fact]
    public async Task SavedOrderCanInterleaveTaskbarAndInfinityPins()
    {
        LaunchableApplication edge = new("edge", "Edge");
        LaunchableApplication explorer = new("explorer", "File Explorer");
        LaunchableApplication terminal = new("terminal", "Terminal");
        DesktopApplicationDockCatalog catalog = new(new TestPickerCatalog([edge, explorer, terminal]), new TestTaskbarPins([edge, explorer]), new TestPinStore([terminal]), new TestOrderStore(["terminal", "explorer", "edge"]), NullLogger<DesktopApplicationDockCatalog>.Instance);
        IReadOnlyList<DesktopApplicationDockEntry> applications = await catalog.GetApplicationsAsync(3);
        Assert.Equal([terminal, explorer, edge], applications.Select(entry => entry.Application));
    }


    private sealed class TestPickerCatalog(IReadOnlyList<LaunchableApplication> applications) : IDesktopApplicationPickerCatalog
    {
        public Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default) => Task.FromResult(applications);

        public Task<ApplicationIcon?> GetIconAsync(LaunchableApplication application, CancellationToken cancellationToken = default) => Task.FromResult<ApplicationIcon?>(null);
    }


    private sealed class TestTaskbarPins(IReadOnlyList<LaunchableApplication> applications) : ITaskbarPinnedApplicationSource
    {
        public Task<IReadOnlyList<LaunchableApplication>> GetPinnedApplicationsAsync(IReadOnlyList<LaunchableApplication> availableApplications, CancellationToken cancellationToken = default) => Task.FromResult(applications);
    }


    private sealed class TestPinStore(IReadOnlyList<LaunchableApplication> applications) : IDesktopApplicationPinStore
    {
        public event Action? PinsChanged;

        public IReadOnlyList<LaunchableApplication> Applications => applications;

        public Task PinAsync(LaunchableApplication application, CancellationToken cancellationToken = default)
        {
            PinsChanged?.Invoke();
            return Task.CompletedTask;
        }


        public Task UnpinAsync(LaunchableApplication application, CancellationToken cancellationToken = default)
        {
            PinsChanged?.Invoke();
            return Task.CompletedTask;
        }
    }


    private sealed class TestOrderStore(IReadOnlyList<string> identifiers) : IDesktopApplicationDockOrderStore
    {
        public IReadOnlyList<string> ApplicationIdentifiers => identifiers;

        public Task SaveAsync(IEnumerable<string> applicationIdentifiers, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
