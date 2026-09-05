using Infinity.Shell;

namespace Infinity.Tests;

public sealed class WindowTitleFilterTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankFilterIsInactiveAndMatchesEveryTitle(string filterValue) => Assert.True(WindowTitleFilter.Matches("Any window", filterValue));

    [Theory]
    [InlineData("visual", "Visual Studio Code")]
    [InlineData("STUDIO code", "Visual Studio Code")]
    [InlineData("vsc", "Visual Studio Code")]
    [InlineData("meb", "Microsoft—Edge Browser")]
    public void FilterMatchesSubstringsMultipleTermsAndAcronyms(string filterValue, string title) => Assert.True(WindowTitleFilter.Matches(title, filterValue));

    [Theory]
    [InlineData("visual missing", "Visual Studio Code")]
    [InlineData("vsz", "Visual Studio Code")]
    [InlineData("too long acronym", "Two Words")]
    public void FilterRejectsTitlesThatDoNotMatchEveryTerm(string filterValue, string title) => Assert.False(WindowTitleFilter.Matches(title, filterValue));
}
