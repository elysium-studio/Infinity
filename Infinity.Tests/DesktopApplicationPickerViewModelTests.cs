using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopApplicationPickerViewModelTests
{
    [Fact]
    public async Task LoadTargetsTheRequestedPageWithoutItsLayout()
    {
        DesktopApplicationPickerViewModel viewModel = CreateViewModel();
        await viewModel.LoadAsync(new DesktopApplicationTarget(3, DesktopSnapLayoutKind.Halves));
        Assert.Equal(new DesktopApplicationTarget(3), viewModel.Target);
    }


    [Fact]
    public async Task SearchFiltersApplicationsWithoutChangingCatalogueOrder()
    {
        DesktopApplicationPickerViewModel viewModel = CreateViewModel();
        await viewModel.LoadAsync(new DesktopApplicationTarget(0));
        viewModel.SearchText = "calc";
        DesktopApplicationPickerItemViewModel application = Assert.Single(viewModel.Results);
        Assert.Equal("Calculator", application.DisplayName);
        Assert.True(viewModel.HasResults);
        Assert.False(viewModel.ShowEmptyState);
    }


    [Fact]
    public async Task LoadDoesNotRequestIconsUntilAnItemIsRealized()
    {
        TestApplicationCatalog catalog = new();
        DesktopApplicationPickerViewModel viewModel = new(new DesktopApplicationPickerCatalog(catalog), new TestDispatcher());
        await viewModel.LoadAsync(new DesktopApplicationTarget(0));
        Assert.Equal(0, catalog.IconRequestCount);
        DesktopApplicationPickerItemViewModel item = Assert.Single(viewModel.Results, result => result.DisplayName == "Calculator");
        await viewModel.LoadIconAsync(item);
        Assert.Equal(1, catalog.IconRequestCount);
        Assert.NotNull(item.Icon);
        await viewModel.LoadIconAsync(item);
        Assert.Equal(1, catalog.IconRequestCount);
    }


    private static DesktopApplicationPickerViewModel CreateViewModel() => new(new DesktopApplicationPickerCatalog(new TestApplicationCatalog()), new TestDispatcher());

    private sealed class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }


    private sealed class TestApplicationCatalog : IApplicationCatalog
    {
        public int IconRequestCount { get; private set; }


        public Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LaunchableApplication>>([new("edge", "Microsoft Edge"), new("calculator", "Calculator"), new("paint", "Paint")]);

        public Task<ApplicationIcon?> GetIconAsync(LaunchableApplication application, CancellationToken cancellationToken = default)
        {
            IconRequestCount++;
            return Task.FromResult<ApplicationIcon?>(new ApplicationIcon(1, 1, [0, 0, 0, 255]));
        }
    }
}
