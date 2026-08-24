using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class TrackedWindowFilterTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankFilterIsInactiveAndMatchesEveryTitle(string filterValue)
    {
        TrackedWindowFilter filter = new() { Filter = filterValue };

        Assert.False(filter.IsActive);
        Assert.True(filter.IsMatch("Any window"));
    }

    [Theory]
    [InlineData("visual", "Visual Studio Code")]
    [InlineData("STUDIO code", "Visual Studio Code")]
    [InlineData("vsc", "Visual Studio Code")]
    [InlineData("meb", "Microsoft—Edge Browser")]
    public void FilterMatchesSubstringsMultipleTermsAndAcronyms(string filterValue, string title)
    {
        TrackedWindowFilter filter = new() { Filter = filterValue };

        Assert.True(filter.IsActive);
        Assert.True(filter.IsMatch(title));
    }

    [Theory]
    [InlineData("visual missing", "Visual Studio Code")]
    [InlineData("vsz", "Visual Studio Code")]
    [InlineData("too long acronym", "Two Words")]
    public void FilterRejectsTitlesThatDoNotMatchEveryTerm(string filterValue, string title)
    {
        TrackedWindowFilter filter = new() { Filter = filterValue };

        Assert.False(filter.IsMatch(title));
    }
}

public sealed class FilterPeekSourceTests
{
    [Fact]
    public void StateAndWindowMatchingAreDelegatedToFilterState()
    {
        StubFilterState filter = new() { Filter = "match", IsActive = true };
        FilterPeekSource source = new(filter);
        TrackedWindow matching = CreateWindow("This is a match");
        TrackedWindow other = CreateWindow("Other window");

        Assert.True(source.IsActive);
        Assert.True(source.RevealsWindow(matching));
        Assert.False(source.RevealsWindow(other));
    }

    private static TrackedWindow CreateWindow(string title) => new()
    {
        Handle = new IntPtr(1),
        CanvasX = 0,
        CanvasY = 0,
        Width = 100,
        Height = 100,
        Title = title
    };

    private sealed class StubFilterState :
        IWindowFilterState
    {
        public bool IsActive { get; init; }

        public string Filter { get; set; } = string.Empty;

        public bool IsMatch(string title) => title.Contains(Filter, StringComparison.OrdinalIgnoreCase);
    }
}