namespace Infinity.Shell;

public sealed class DesktopOverviewConfiguration
{
    public DesktopOverviewBackdrop Backdrop { get; set; } = DesktopOverviewBackdrop.Wallpaper;

    public bool IsEdgeScrollingEnabled { get; set; } = true;

    public bool IsMonitorSpanningEnabled { get; set; } = true;

    public bool IsSnapAssistanceEnabled { get; set; } = true;
}
