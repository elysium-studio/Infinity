using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopPageTitleViewModelTests
{
    private readonly DesktopPageTitleViewModel viewModel = new(new DesktopPageEditorLabels(string.Empty, string.Empty, string.Empty, string.Empty), new DesktopSnapLayoutCatalog());

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
    public void SelectedLayoutHighlightsEverySlot()
    {
        viewModel.ConfigureDisplay(1920, 1080, 1);
        viewModel.Bind(0, string.Empty, DesktopSnapLayoutKind.Thirds);

        DesktopSnapLayoutOptionViewModel selected = Assert.Single(viewModel.AvailableLayouts, option => option.IsSelected);

        Assert.Equal(DesktopSnapLayoutKind.Thirds, selected.Kind);
        Assert.All(selected.Slots, slot => Assert.True(slot.IsHighlighted));
    }
}
