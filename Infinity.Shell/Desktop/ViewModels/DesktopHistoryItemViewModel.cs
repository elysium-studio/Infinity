namespace Infinity.Shell;

public class DesktopHistoryItemViewModel(long id,
    string title,
    string pageLabel,
    string visitedLabel,
    string glyph,
    Action<long> navigate)
{
    public string Glyph { get; } = glyph;

    public string PageLabel { get; } = pageLabel;

    public string Title { get; } = title;

    public string VisitedLabel { get; } = visitedLabel;

    public void Navigate() => navigate(id);
}
