using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopApplicationDockViewModelTests
{
    [Fact]
    public async Task SaveOrderPersistsTheListViewCollectionOrder()
    {
        LaunchableApplication first = new("first", "First");
        LaunchableApplication second = new("second", "Second");
        LaunchableApplication third = new("third", "Third");
        LaunchableApplication fourth = new("fourth", "Fourth");
        TestOrderStore orderStore = new();
        DesktopApplicationDockViewModel viewModel = new(
            new TestPinStore([first, second, third, fourth]),
            orderStore,
            new PendingDockCatalog(),
            new TestApplicationCatalog(),
            new TestDispatcher());

        viewModel.Applications.Move(0, 2);
        viewModel.Applications.Move(3, 1);
        await viewModel.SaveOrderAsync();

        Assert.Equal(
            [second.Id, fourth.Id, third.Id, first.Id],
            viewModel.Applications.Select(item => item.Application.Id));
        Assert.Equal(
            [second.Id, fourth.Id, third.Id, first.Id],
            orderStore.ApplicationIdentifiers);
    }

    private sealed class TestPinStore(IReadOnlyList<LaunchableApplication> applications) :
        IDesktopApplicationPinStore
    {
        public event Action? PinsChanged;

        public IReadOnlyList<LaunchableApplication> Applications => applications;

        public Task PinAsync(
            LaunchableApplication application,
            CancellationToken cancellationToken = default)
        {
            PinsChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task UnpinAsync(
            LaunchableApplication application,
            CancellationToken cancellationToken = default)
        {
            PinsChanged?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class TestOrderStore : IDesktopApplicationDockOrderStore
    {
        public IReadOnlyList<string> ApplicationIdentifiers { get; private set; } = [];

        public Task SaveAsync(
            IEnumerable<string> applicationIdentifiers,
            CancellationToken cancellationToken = default)
        {
            ApplicationIdentifiers = [.. applicationIdentifiers];
            return Task.CompletedTask;
        }
    }

    private sealed class PendingDockCatalog : IDesktopApplicationDockCatalog
    {
        public Task<IReadOnlyList<DesktopApplicationDockEntry>> GetApplicationsAsync(
            int maximumCount,
            CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<IReadOnlyList<DesktopApplicationDockEntry>>().Task;
    }

    private sealed class TestApplicationCatalog : IDesktopApplicationPickerCatalog
    {
        public Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LaunchableApplication>>([]);

        public Task<ApplicationIcon?> GetIconAsync(
            LaunchableApplication application,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ApplicationIcon?>(null);
    }

    private sealed class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }
}
