using System;

namespace Infinity.UI.WinUI;

[Flags]
internal enum DesktopOverlayExtendedWindowStyle : uint
{
    Transparent = 0x00000020,
    ToolWindow = 0x00000080,
    Layered = 0x00080000,
    NoActivate = 0x08000000
}
