namespace Infinity.Shell;

public sealed record DesktopPageReorderPreviewState(int SourcePage, int TargetPage, double HorizontalDelta, bool IsGapOpen = true)
{
    public int MapPage(int page)
    {
        if (IsGapOpen)
        {
            return PageReorderMapping.Map(page, SourcePage, TargetPage);
        }

        if (TargetPage > SourcePage && page > SourcePage)
        {
            return page - 1;
        }

        if (TargetPage < SourcePage && page < SourcePage)
        {
            return page + 1;
        }

        return page;
    }
}
