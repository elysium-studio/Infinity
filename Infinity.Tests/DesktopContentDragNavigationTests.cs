using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopContentDragNavigationTests
{
    [Fact]
    public void HoveringNeverSelectsWhileModifiersAreHeld()
    {
        DesktopContentDragNavigation navigation = new();
        DesktopContentDragTarget window = new(2, 123);
        for (int update = 0; update < 1000; update++)
        {
            Assert.Equal(DesktopContentDragTarget.None, navigation.Update(true, window));
        }
    }

    [Fact]
    public void ReleasingModifiersSelectsTheCurrentWindow()
    {
        DesktopContentDragNavigation navigation = new();
        navigation.Update(true, new(2, 123));
        DesktopContentDragTarget current = new(3, 456);
        Assert.Equal(current, navigation.Update(false, current));
    }

    [Fact]
    public void ReleasingModifiersOverPageBackgroundSelectsThePage()
    {
        DesktopContentDragNavigation navigation = new();
        DesktopContentDragTarget page = new(3, 0);
        navigation.Update(true, page);
        Assert.Equal(page, navigation.Update(false, page));
    }

    [Fact]
    public void ReleaseIsOnlyHandledOnce()
    {
        DesktopContentDragNavigation navigation = new();
        DesktopContentDragTarget window = new(2, 123);
        navigation.Update(true, window);
        Assert.Equal(window, navigation.Update(false, window));
        Assert.Equal(DesktopContentDragTarget.None, navigation.Update(false, window));
    }

    [Fact]
    public void ReleaseOutsideATargetDoesNotSelectThePreviousWindow()
    {
        DesktopContentDragNavigation navigation = new();
        navigation.Update(true, new(2, 123));
        Assert.Equal(DesktopContentDragTarget.None, navigation.Update(false, DesktopContentDragTarget.None));
    }

    [Fact]
    public void ResetDisarmsSelection()
    {
        DesktopContentDragNavigation navigation = new();
        DesktopContentDragTarget window = new(2, 123);
        navigation.Update(true, window);
        navigation.Reset();
        Assert.Equal(DesktopContentDragTarget.None, navigation.Update(false, window));
    }

    [Fact]
    public void ReleaseWithoutHoldingDoesNotSelect()
    {
        DesktopContentDragNavigation navigation = new();
        Assert.Equal(DesktopContentDragTarget.None, navigation.Update(false, new(2, 123)));
    }

    [Theory]
    [InlineData("StorageItems", ContentDragKind.Files)]
    [InlineData("FileGroupDescriptorW", ContentDragKind.VirtualFiles)]
    [InlineData("FileContents", ContentDragKind.VirtualFiles)]
    [InlineData("Text", ContentDragKind.Text)]
    [InlineData("HTML Format", ContentDragKind.Text)]
    [InlineData("Rich Text Format", ContentDragKind.Text)]
    [InlineData("WebLink", ContentDragKind.Link)]
    [InlineData("ApplicationLink", ContentDragKind.Link)]
    [InlineData("Bitmap", ContentDragKind.Image)]
    [InlineData("Application.CustomFormat", ContentDragKind.Other)]
    public void ClassifiesFormatsWithoutReadingPayloads(string format, ContentDragKind expected) => Assert.Equal(expected, DesktopContentDragFormats.Classify([format]));

    [Fact]
    public void KeepsAllAvailableContentKinds()
    {
        ContentDragKind result = DesktopContentDragFormats.Classify(["StorageItems", "Text", "Application.CustomFormat"]);
        Assert.Equal(ContentDragKind.Files | ContentDragKind.Text | ContentDragKind.Other, result);
    }

    [Fact]
    public void UnknownPayloadsCanStillNavigate() => Assert.Equal(ContentDragKind.Other, DesktopContentDragFormats.Classify([]));
}
