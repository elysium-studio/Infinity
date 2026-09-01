using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class TaskbarPinnedApplicationMatcherTests
{
    [Fact]
    public void MatchUsesShortcutNamesAndPreservesTheirOrder()
    {
        LaunchableApplication edge = new("edge", "Microsoft Edge");
        LaunchableApplication explorer = new("explorer", "File Explorer");

        IReadOnlyList<LaunchableApplication> result = TaskbarPinnedApplicationMatcher.Match(
            [@"C:\Pins\Microsoft   Edge.lnk", @"C:\Pins\File Explorer.lnk"],
            [explorer, edge]);

        Assert.Equal([edge, explorer], result);
    }

    [Fact]
    public void MatchIgnoresUnknownAndDuplicateShortcuts()
    {
        LaunchableApplication edge = new("edge", "Microsoft Edge");

        IReadOnlyList<LaunchableApplication> result = TaskbarPinnedApplicationMatcher.Match(
            ["Microsoft Edge.lnk", "Unknown.lnk", "Microsoft Edge.lnk"],
            [edge]);

        Assert.Equal([edge], result);
    }
}
