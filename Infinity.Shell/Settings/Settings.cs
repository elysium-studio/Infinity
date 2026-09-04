using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class Settings
{
    public List<string>? DockApplicationOrder { get; set; } = [];

    public DragScrollSpeed DragScrollSpeed { get; set; } = DragScrollSpeed.Normal;

    public bool EnableOverviewEdgeScrolling { get; set; } = true;

    public bool EnableSnapAssistance { get; set; } = true;

    public DesktopOverviewBackdrop OverviewBackdrop { get; set; } = DesktopOverviewBackdrop.Wallpaper;

    public Dictionary<int, DesktopSnapLayoutKind>? PageLayouts { get; set; } = [];

    public Dictionary<int, string>? PageTitles { get; set; } = [];

    public List<LaunchableApplication>? PinnedApplications { get; set; } = [];

    public List<LaunchableApplication>? RecentApplications { get; set; } = [];

    public List<List<int>> ScrollModifierKeys { get; set; } =
        [
            [VirtualKeys.VK_LWIN, VirtualKeys.VK_RWIN],
            [VirtualKeys.VK_LCONTROL, VirtualKeys.VK_RCONTROL]
        ];

    public ScrollSpeed ScrollSpeed { get; set; } = ScrollSpeed.Normal;

    public bool ShowHintOnStartup { get; set; } = true;

    public bool ShowOverviewApplicationDock { get; set; } = true;

    public bool ShowOverviewKeyboardShortcutButton { get; set; } = true;

    public bool ShowOverviewClock { get; set; } = true;

    public bool ShowOverviewPageHeaders { get; set; } = true;

    public bool ShowOverviewSearchBox { get; set; } = true;

    public bool SpanCompatibleDisplays { get; set; } = true;

    public bool StartWithWindows { get; set; } = true;

    public double VirtualPagesCount { get; set; } = 10;

    public VirtualPagesMode VirtualPagesMode { get; set; } = VirtualPagesMode.Unlimited;
}
