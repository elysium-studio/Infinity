using Infinity.Shell;
using Infinity.Platform.Abstractions;

namespace Infinity.Tests;

public sealed class DesktopWindowContentIndexTests
{
    private readonly DesktopWindowContentIndex index = new();

    [Fact]
    public void PendingContentMatchRemainsVisibleBeforeAndAfterRecognition()
    {
        index.SetSearchQuery("invoice");
        Assert.True(index.ShouldDisplay(1, "Editor", "invoice", true));
        Assert.False(index.Matches(1, "Editor", "invoice"));
        index.TryUpdateSnapshot(1, "a", new(200, 100, new byte[80000]), new("invoice", []));
        Assert.True(index.ShouldDisplay(1, "Editor", "invoice", true));
        Assert.True(index.Matches(1, "Editor", "invoice"));
    }

    [Fact]
    public void OnlyConfirmedNonMatchesAreHiddenDuringContentSearch()
    {
        Assert.True(index.ShouldDisplay(1, "Editor", "invoice", true));
        index.Update(1, "a", 200, 100, "receipt");
        Assert.False(index.ShouldDisplay(1, "Editor", "invoice", true));
    }

    [Fact]
    public void TitleOnlySearchDoesNotWaitForRecognition()
    {
        Assert.False(index.ShouldDisplay(1, "Editor", "invoice", false));
        Assert.True(index.ShouldDisplay(1, "Invoice editor", "invoice", false));
    }

    [Fact]
    public void FailedRecognitionDoesNotHideAPendingWindow()
    {
        index.SetSearchQuery("invoice");
        Assert.False(index.TryUpdateSnapshot(1, null, null, null));
        Assert.True(index.ShouldDisplay(1, "Editor", "invoice", true));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("invoice", true)]
    [InlineData("INVOICE 74219", true)]
    [InlineData("editor invoice", true)]
    [InlineData("other invoice", false)]
    public void SearchesTitleAndCurrentContentTogether(string query, bool expected)
    {
        index.Update(1, "a", 200, 100, "Invoice 74219 due tomorrow");
        Assert.Equal(expected, index.Matches(1, "Editor", query));
    }

    [Fact]
    public void TitleSearchStillWorksBeforeSnapshotArrives() => Assert.True(index.Matches(1, "Visual Studio Code", "vsc"));

    [Fact]
    public void RefreshReplacesTextRatherThanKeepingHistory()
    {
        index.Update(1, "a", 200, 100, "old invoice");
        index.Update(1, "b", 200, 100, "new receipt");
        Assert.False(index.Matches(1, "Editor", "invoice"));
        Assert.True(index.Matches(1, "Editor", "receipt"));
    }

    [Fact]
    public void IdenticalPixelsAndSizeSkipRecognition()
    {
        index.Update(1, "a", 200, 100, "invoice");
        Assert.True(index.IsUnchanged(1, "a", 200, 100));
        Assert.False(index.IsUnchanged(1, "b", 200, 100));
        Assert.False(index.IsUnchanged(1, "a", 100, 200));
        Assert.False(index.IsUnchanged(2, "a", 200, 100));
    }

    [Fact]
    public void RemovingClosedWindowCannotMatchItsOldContent()
    {
        index.Update(1, "a", 200, 100, "private note");
        index.Remove(1);
        Assert.False(index.Matches(1, "New window", "private"));
        Assert.Null(index.GetSnippet(1, "private"));
    }

    [Fact]
    public void ClosingOverlayClearsEveryEntry()
    {
        index.Update(1, "a", 200, 100, "first note");
        index.Update(2, "b", 200, 100, "second note");
        index.Clear();
        Assert.False(index.Matches(1, "Editor", "note"));
        Assert.False(index.Matches(2, "Editor", "note"));
    }

    [Fact]
    public void FailedFreshCaptureKeepsLastSuccessfulResult()
    {
        WindowContentSnapshot image = new(200, 100, new byte[80000]);
        index.TryUpdateSnapshot(1, "a", image, new("invoice", []));
        DesktopWindowSearchSnapshot? snapshot = index.GetSnapshot(1);
        Assert.False(index.TryUpdateSnapshot(1, null, null, null));
        Assert.True(index.IsUnchanged(1, "a", 200, 100));
        Assert.True(index.Matches(1, "Editor", "invoice"));
        Assert.Same(snapshot, index.GetSnapshot(1));
    }

    [Fact]
    public void SearchKeepsExistingResultsStableUntilQueryIsCleared()
    {
        WindowContentSnapshot image = new(200, 100, new byte[80000]);
        index.TryUpdateSnapshot(1, "a", image, new("invoice", []));
        DesktopWindowSearchSnapshot? snapshot = index.GetSnapshot(1);
        index.SetSearchQuery("invoice");
        Assert.False(index.CanRefresh(1));
        Assert.False(index.TryUpdateSnapshot(1, "b", image, new("receipt", [])));
        Assert.True(index.Matches(1, "Editor", "invoice"));
        Assert.Same(snapshot, index.GetSnapshot(1));
        index.SetSearchQuery("invo");
        Assert.False(index.CanRefresh(1));
        index.SetSearchQuery("");
        Assert.True(index.CanRefresh(1));
        Assert.True(index.TryUpdateSnapshot(1, "b", image, new("receipt", [])));
        Assert.False(index.Matches(1, "Editor", "invoice"));
    }

    [Fact]
    public void WindowsWithoutAnInitialResultCanStillBeIndexedDuringSearch()
    {
        index.SetSearchQuery("invoice");
        Assert.True(index.CanRefresh(1));
        Assert.True(index.TryUpdateSnapshot(1, "a", new(200, 100, new byte[80000]), new("invoice", [])));
        Assert.False(index.CanRefresh(1));
        Assert.True(index.Matches(1, "Editor", "invoice"));
        index.Remove(1);
        Assert.False(index.Matches(1, "Editor", "invoice"));
        Assert.True(index.CanRefresh(1));
    }

    [Fact]
    public void SnippetIncludesMatchAndStaysBounded()
    {
        index.Update(1, "a", 200, 100, new string('x', 500) + " invoice\r\n74219 " + new string('x', 500));
        string snippet = Assert.IsType<string>(index.GetSnippet(1, "invoice"));
        Assert.Contains("invoice", snippet);
        Assert.DoesNotContain('\r', snippet);
        Assert.DoesNotContain('\n', snippet);
        Assert.True(snippet.Length <= 182);
    }

    [Fact]
    public void EmptyQueryDoesNotExposeContentInTooltip()
    {
        index.Update(1, "a", 200, 100, "private note");
        Assert.Null(index.GetSnippet(1, ""));
        Assert.Null(index.GetSnippet(1, "missing"));
    }

    [Fact]
    public void TextStorageIsBounded()
    {
        index.Update(1, "a", 200, 100, new string('x', 65536) + "not retained");
        Assert.False(index.Matches(1, "Editor", "retained"));
    }
}
