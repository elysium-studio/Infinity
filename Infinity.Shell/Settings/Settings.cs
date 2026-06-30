using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public class Settings
{
    public bool DesktopBlur { get; set; } = true;

    public DragScrollSpeed DragScrollSpeed { get; set; } = DragScrollSpeed.Normal;

    public bool HideFilteredWindows { get; set; } = false;

    public Dictionary<int, string>? PageTitles { get; set; } = [];

    public PreviewPosition PreviewPosition { get; set; } = PreviewPosition.Bottom;

    public PreviewSize PreviewSize { get; set; } = PreviewSize.Default;

    public List<List<int>> ScrollModifierKeys { get; set; } =
        [
            [VirtualKeys.VK_LWIN, VirtualKeys.VK_RWIN],
            [VirtualKeys.VK_LCONTROL, VirtualKeys.VK_RCONTROL]
        ];

    public ScrollSpeed ScrollSpeed { get; set; } = ScrollSpeed.Normal;

    public bool ShowDesktopBackground { get; set; } = true;

    public bool ShowHintOnStartup { get; set; } = true;

    public bool StartWithWindows { get; set; } = true;

    public double VirtualPagesCount { get; set; } = 10;

    public VirtualPagesMode VirtualPagesMode { get; set; } = VirtualPagesMode.Unlimited;
}