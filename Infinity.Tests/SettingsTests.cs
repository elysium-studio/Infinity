using Infinity.Shell;

namespace Infinity.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void DesktopOverviewFeaturesAreEnabledByDefault()
    {
        Settings settings = new();

        Assert.True(settings.EnableOverviewEdgeScrolling);
        Assert.True(settings.EnableSnapAssistance);
        Assert.True(settings.SpanCompatibleDisplays);
        Assert.Equal(DesktopOverviewBackdrop.Wallpaper, settings.OverviewBackdrop);
    }
}
