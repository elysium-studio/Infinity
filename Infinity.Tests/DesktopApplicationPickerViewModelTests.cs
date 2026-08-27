using Elysium.Application.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopApplicationPickerViewModelTests
{
    [Fact]
    public async Task LoadBuildsPageAndLayoutSlotDestinations()
    {
        DesktopApplicationPickerViewModel viewModel = CreateViewModel();

        await viewModel.LoadAsync(new DesktopApplicationTarget(3, DesktopSnapLayoutKind.Halves));

        Assert.Equal(3, viewModel.Destinations.Count);
        Assert.Equal(new DesktopApplicationTarget(3), viewModel.Destinations[0].Target);
        Assert.Equal(new DesktopApplicationTarget(3, DesktopSnapLayoutKind.Halves, 0), viewModel.Destinations[1].Target);
        Assert.Equal(new DesktopApplicationTarget(3, DesktopSnapLayoutKind.Halves, 1), viewModel.Destinations[2].Target);
    }

    [Fact]
    public async Task SearchFiltersApplicationsWithoutChangingCatalogueOrder()
    {
        DesktopApplicationPickerViewModel viewModel = CreateViewModel();
        await viewModel.LoadAsync(new DesktopApplicationTarget(0));

        viewModel.SearchText = "calc";

        LaunchableApplication application = Assert.Single(viewModel.Results);
        Assert.Equal("Calculator", application.DisplayName);
        Assert.True(viewModel.HasResults);
        Assert.False(viewModel.ShowEmptyState);
    }

    private static DesktopApplicationPickerViewModel CreateViewModel() => new(new TestApplicationCatalog(), new DesktopSnapLayoutCatalog(), new TestLocalizer(), new TestDispatcher());

    private sealed class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }

    private sealed class TestApplicationCatalog : IApplicationCatalog
    {
        public Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LaunchableApplication>>([
                new("edge", "Microsoft Edge"),
                new("calculator", "Calculator"),
                new("paint", "Paint")
            ]);
    }

    private sealed class TestLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments)
        {
            string value = key switch
            {
                "DesktopAppPickerPageDestination" => "On this page",
                "DesktopAppPickerSlotDestination" => "Slot {0}",
                _ => key
            };

            return arguments.Length > 0 ? string.Format(value, arguments) : value;
        }
    }
}
