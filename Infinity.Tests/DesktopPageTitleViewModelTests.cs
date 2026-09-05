using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopPageTitleViewModelTests
{
    private readonly DesktopPageTitleViewModel viewModel = new(new DesktopPageEditorLabels(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty), new DesktopSnapLayoutCatalog());

    [Fact]
    public void ArrangeRequestsCurrentConfiguredLayout()
    {
        DesktopPageTitleViewModel? requested = null;
        viewModel.Bind(2, "Work", DesktopSnapLayoutKind.Halves);
        viewModel.ArrangeRequested += source => requested = source;
        viewModel.Arrange();
        Assert.Same(viewModel, requested);
    }


    [Fact]
    public void ArrangeWithoutLayoutDoesNothing()
    {
        bool requested = false;
        viewModel.ArrangeRequested += _ => requested = true;
        viewModel.Arrange();
        Assert.False(requested);
    }


    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(3440, 1440)]
    [InlineData(1080, 1920)]
    public void LayoutPreviewsMatchDisplayAspectRatio(double width, double height)
    {
        viewModel.ConfigureDisplay(width, height, 1);
        Assert.NotEmpty(viewModel.AvailableLayouts);
        foreach (DesktopSnapLayoutOptionViewModel option in viewModel.AvailableLayouts)
        {
            Assert.Equal(width / height, option.PreviewWidth / option.PreviewHeight, 10);
        }
    }


    [Fact]
    public void SelectedLayoutSelectsMatchingOption()
    {
        viewModel.ConfigureDisplay(1920, 1080, 1);
        viewModel.Bind(0, string.Empty, DesktopSnapLayoutKind.Thirds);
        DesktopSnapLayoutOptionViewModel selected = Assert.Single(viewModel.AvailableLayouts, option => option.IsSelected);
        Assert.Equal(DesktopSnapLayoutKind.Thirds, selected.Kind);
        Assert.All(selected.Slots, slot => Assert.True(slot.IsSelected));
    }


    [Fact]
    public void ClearLayoutSubmitsNoneAndRemovesSelection()
    {
        viewModel.ConfigureDisplay(1920, 1080, 1);
        viewModel.Bind(2, string.Empty, DesktopSnapLayoutKind.Thirds);
        DesktopSnapLayoutKind? submittedLayout = null;
        viewModel.LayoutSubmitted += (_, layout) => submittedLayout = layout;
        viewModel.ClearLayout();
        Assert.Equal(DesktopSnapLayoutKind.None, viewModel.Layout);
        Assert.Equal(DesktopSnapLayoutKind.None, submittedLayout);
        Assert.False(viewModel.HasLayout);
        Assert.DoesNotContain(viewModel.AvailableLayouts, option => option.IsSelected);
    }


    [Fact]
    public void SelectingCurrentLayoutArrangesAgainWithoutRewritingSettings()
    {
        viewModel.Bind(2, string.Empty, DesktopSnapLayoutKind.Halves);
        int arrangements = 0;
        int submissions = 0;
        viewModel.ArrangeRequested += _ => arrangements++;
        viewModel.LayoutSubmitted += (_, _) => submissions++;
        viewModel.SelectLayout(DesktopSnapLayoutKind.Halves);
        Assert.Equal(1, arrangements);
        Assert.Equal(0, submissions);
    }


    [Fact]
    public void ChangingLayoutSubmitsNewChoiceWithoutADuplicateArrangeRequest()
    {
        viewModel.Bind(2, string.Empty, DesktopSnapLayoutKind.Halves);
        DesktopSnapLayoutKind? submitted = null;
        int arrangements = 0;
        viewModel.LayoutSubmitted += (_, layout) => submitted = layout;
        viewModel.ArrangeRequested += _ => arrangements++;
        viewModel.SelectLayout(DesktopSnapLayoutKind.Quarters);
        Assert.Equal(DesktopSnapLayoutKind.Quarters, submitted);
        Assert.Equal(0, arrangements);
    }
}
