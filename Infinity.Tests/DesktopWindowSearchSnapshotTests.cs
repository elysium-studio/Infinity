using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowSearchSnapshotTests
{
    private static readonly WindowTextRecognition Recognition = new("Invoice 74219 invoice", [new("Invoice", 10, 20, 80, 12), new("74219", 100, 20, 40, 12), new("invoice", 10, 40, 80, 12)]);

    [Fact]
    public void FindsAllWordOccurrencesAndPreservesTheirCoordinates()
    {
        DesktopWindowSearchSnapshot snapshot = new(new(200, 100, []), Recognition);
        WindowTextWord[] matches = snapshot.GetMatches("INVOICE");
        Assert.Equal(2, matches.Length);
        Assert.Equal(20, matches[0].Y);
        Assert.Equal(40, matches[1].Y);
    }

    [Fact]
    public void MatchesMultipleQueryWordsWithoutDuplicateHighlights()
    {
        DesktopWindowSearchSnapshot snapshot = new(new(200, 100, []), Recognition);
        Assert.Equal(3, snapshot.GetMatches("invoice INV 74219").Length);
        Assert.Empty(snapshot.GetMatches(" "));
    }

    [Fact]
    public void SnapshotAndCoordinatesAreReplacedTogether()
    {
        DesktopWindowContentIndex index = new();
        WindowContentSnapshot first = new(200, 100, new byte[80000]);
        WindowContentSnapshot second = new(100, 200, new byte[80000]);
        index.Update(42, "first", first, Recognition);
        WindowTextRecognition replacement = new("receipt", [new("receipt", 50, 100, 30, 15)]);
        index.Update(42, "second", second, replacement);
        DesktopWindowSearchSnapshot snapshot = Assert.IsType<DesktopWindowSearchSnapshot>(index.GetSnapshot(42));
        Assert.Same(second, snapshot.Image);
        Assert.Same(replacement, snapshot.Recognition);
        Assert.Empty(snapshot.GetMatches("invoice"));
        Assert.Single(snapshot.GetMatches("receipt"));
        index.Clear();
        Assert.Null(index.GetSnapshot(42));
        Assert.Null(index.GetRecognition(42));
    }

    [Fact]
    public void SnapshotBudgetEvictsImagesButRetainsSearchableText()
    {
        DesktopWindowContentIndex index = new();
        WindowContentSnapshot image = new(2500, 2500, new byte[2500 * 2500 * 4]);
        index.Update(1, "a", image, Recognition);
        index.Update(2, "a", image, Recognition);
        index.Update(3, "a", image, Recognition);
        Assert.Null(index.GetSnapshot(1));
        Assert.NotNull(index.GetSnapshot(2));
        Assert.NotNull(index.GetSnapshot(3));
        Assert.True(index.Matches(1, "Editor", "invoice"));
        Assert.Same(Recognition, index.GetRecognition(1));
        index.Remove(2);
        Assert.Null(index.GetSnapshot(2));
        Assert.Null(index.GetRecognition(2));
    }
}
